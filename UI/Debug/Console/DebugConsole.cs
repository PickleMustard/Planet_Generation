#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace UI.Debug.Console;

public partial class DebugConsole : BaseDebugModule
{
    private const int MaxVisibleSuggestions = 10;
    private const int SuggestionItemHeight = 24;

    private CommandRegistry? _registry;
    private AutocompleteEngine? _autocompleteEngine;
    private LineEdit? _inputField;
    private RichTextLabel? _outputDisplay;
    private PanelContainer? _autocompletePanel;
    private ScrollContainer? _autocompleteScroll;
    private VBoxContainer? _autocompleteList;
    private readonly List<Suggestion> _currentSuggestions = new();
    private int _selectedSuggestionIndex = -1;
    private readonly List<Label> _suggestionLabels = new();
    private bool _autocompleteVisible;

    public override string ModuleName => "Console";

    public override void _Ready()
    {
        base._Ready();
        BuildUI();
        InitializeRegistry();
    }

    private void BuildUI()
    {
        Name = "DebugConsole";
        AnchorRight = 1f;
        AnchorBottom = 1f;
        OffsetRight = 0;
        OffsetBottom = 0;

        var mainContainer = new VBoxContainer
        {
            Name = "MainContainer",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetRight = 0,
            OffsetBottom = 0,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        var outputPanel = new PanelContainer
        {
            Name = "OutputPanel",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        _outputDisplay = new RichTextLabel
        {
            Name = "OutputDisplay",
            BbcodeEnabled = true,
            ScrollFollowing = true,
            SelectionEnabled = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        outputPanel.AddChild(_outputDisplay);

        var inputContainer = new HBoxContainer
        {
            Name = "InputContainer"
        };

        var promptLabel = new Label
        {
            Name = "PromptLabel",
            Text = ">",
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _inputField = new LineEdit
        {
            Name = "InputField",
            PlaceholderText = "Enter command...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ContextMenuEnabled = true,
            ClearButtonEnabled = false
        };
        _inputField.TextSubmitted += OnTextSubmitted;
        _inputField.TextChanged += OnTextChanged;

        inputContainer.AddChild(promptLabel);
        inputContainer.AddChild(_inputField);

        mainContainer.AddChild(outputPanel);
        mainContainer.AddChild(inputContainer);

        BuildAutocompletePopup();
        AddChild(mainContainer);
    }

    private void BuildAutocompletePopup()
    {
        _autocompletePanel = new PanelContainer
        {
            Name = "AutocompletePanel",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 100
        };

        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f),
            BorderColor = new Color(0.3f, 0.3f, 0.3f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        _autocompletePanel.AddThemeStyleboxOverride("panel", styleBox);

        _autocompleteScroll = new ScrollContainer
        {
            Name = "AutocompleteScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _autocompleteList = new VBoxContainer
        {
            Name = "AutocompleteList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _autocompleteScroll.AddChild(_autocompleteList);
        _autocompletePanel.AddChild(_autocompleteScroll);
        AddChild(_autocompletePanel);
    }

    private void InitializeRegistry()
    {
        _registry = new CommandRegistry();
        _registry.Initialize();
        _autocompleteEngine = new AutocompleteEngine(_registry);

        PrintWelcome();
    }

    private void PrintWelcome()
    {
        AppendOutput("[color=green]Debug Console initialized.[/color]");
        AppendOutput("Type [color=cyan]help[/color] for available commands.");
        AppendOutput("Use [color=cyan]Tab[/color] to accept suggestions, [color=cyan]Up/Down[/color] to navigate.");
        AppendOutput("");
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisible)
        {
            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            HandleKeyEvent(keyEvent);
        }
    }

    private void HandleKeyEvent(InputEventKey keyEvent)
    {
        if (_autocompleteVisible && _currentSuggestions.Count > 0)
        {
            switch (keyEvent.Keycode)
            {
                case Key.Up:
                    NavigateSuggestions(-1);
                    GetViewport().SetInputAsHandled();
                    return;
                case Key.Down:
                    NavigateSuggestions(1);
                    GetViewport().SetInputAsHandled();
                    return;
                case Key.Tab:
                    AcceptAutocomplete();
                    GetViewport().SetInputAsHandled();
                    return;
                case Key.Escape:
                    HideAutocomplete();
                    GetViewport().SetInputAsHandled();
                    return;
                case Key.Enter:
                    return;
            }
        }

        switch (keyEvent.Keycode)
        {
            case Key.Up:
                NavigateHistory(-1);
                GetViewport().SetInputAsHandled();
                break;
            case Key.Down:
                NavigateHistory(1);
                GetViewport().SetInputAsHandled();
                break;
            case Key.Tab:
                AcceptAutocomplete();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Escape:
                HideAutocomplete();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void OnTextSubmitted(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _autocompleteEngine!.AddToHistory(text);
        ExecuteCommand(text);
        _inputField!.Clear();
        HideAutocomplete();
    }

    private void OnTextChanged(string newText)
    {
        UpdateAutocomplete(newText);
    }

    private void ExecuteCommand(string input)
    {
        AppendOutput($"[color=gray]> {input}[/color]");

        using var context = new CommandContext(_registry!, targetInstance: this!);
        var exitCode = _registry!.Execute(input, context);

        var output = context.GetOutput();
        if (!string.IsNullOrEmpty(output))
        {
            AppendOutput(output.TrimEnd());
        }

        if (exitCode != 0)
        {
            AppendOutput($"[color=red]Command exited with code: {exitCode}[/color]");
        }
    }

    private void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _outputDisplay!.AppendText("\n");
        }
        else
        {
            _outputDisplay!.AppendText(text + "\n");
        }
    }

    public void ClearOutput()
    {
        _outputDisplay!.Clear();
        PrintWelcome();
    }

    private void NavigateHistory(int direction)
    {
        var history = _autocompleteEngine!.GetHistory().ToList();
        if (history.Count == 0)
        {
            return;
        }

        _selectedSuggestionIndex += direction;

        if (_selectedSuggestionIndex < 0)
        {
            _selectedSuggestionIndex = 0;
        }
        else if (_selectedSuggestionIndex >= history.Count)
        {
            _selectedSuggestionIndex = history.Count;
            _inputField!.Clear();
            return;
        }

        _inputField!.Text = history[_selectedSuggestionIndex];
        _inputField!.CaretColumn = _inputField!.Text.Length;
    }

    private void UpdateAutocomplete(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < 2)
        {
            HideAutocomplete();
            return;
        }

        var suggestions = _autocompleteEngine!.GetSuggestions(input);
        _currentSuggestions.Clear();
        _selectedSuggestionIndex = -1;

        foreach (var suggestion in suggestions)
        {
            _currentSuggestions.Add(suggestion);
        }

        if (_currentSuggestions.Count == 0)
        {
            HideAutocomplete();
            return;
        }

        ShowAutocomplete();
    }

    private void ShowAutocomplete()
    {
        foreach (var child in _autocompleteList!.GetChildren())
        {
            child.QueueFree();
        }
        _suggestionLabels.Clear();

        var maxDisplay = Math.Min(_currentSuggestions.Count, MaxVisibleSuggestions);
        for (int i = 0; i < maxDisplay; i++)
        {
            var suggestion = _currentSuggestions[i];
            var label = new Label
            {
                Text = GetSuggestionDisplayText(suggestion),
                MouseFilter = Control.MouseFilterEnum.Pass,
                CustomMinimumSize = new Vector2(0, SuggestionItemHeight)
            };

            var styleBox = new StyleBoxFlat
            {
                BgColor = new Color(0.15f, 0.15f, 0.15f, 1f)
            };
            label.AddThemeStyleboxOverride("normal", styleBox);

            _autocompleteList!.AddChild(label);
            _suggestionLabels.Add(label);
        }

        UpdateSuggestionHighlight();

        var inputRect = _inputField!.GetGlobalRect();
        var popupHeight = Math.Min(maxDisplay * SuggestionItemHeight, 250);
        var popupSize = new Vector2I((int)inputRect.Size.X, popupHeight);
        var popupPos = new Vector2I((int)inputRect.Position.X, (int)(inputRect.Position.Y - popupHeight));

        _autocompleteScroll!.CustomMinimumSize = new Vector2(popupSize.X, popupSize.Y);
        _autocompleteScroll!.Size = new Vector2(popupSize.X, popupSize.Y);
        _autocompletePanel!.GlobalPosition = popupPos;
        _autocompletePanel!.Size = popupSize;
        _autocompletePanel!.Show();
        _autocompleteVisible = true;
    }

    private string GetSuggestionDisplayText(Suggestion suggestion)
    {
        if (!string.IsNullOrEmpty(suggestion.Description))
        {
            return $"{suggestion.Text} - {suggestion.Description}";
        }
        return suggestion.Text;
    }

    private void HideAutocomplete()
    {
        _autocompletePanel!.Hide();
        _currentSuggestions.Clear();
        _selectedSuggestionIndex = -1;
        _autocompleteVisible = false;
    }

    private void NavigateSuggestions(int direction)
    {
        if (_currentSuggestions.Count == 0)
        {
            return;
        }

        _selectedSuggestionIndex += direction;

        if (_selectedSuggestionIndex < 0)
        {
            _selectedSuggestionIndex = _currentSuggestions.Count - 1;
        }
        else if (_selectedSuggestionIndex >= _currentSuggestions.Count)
        {
            _selectedSuggestionIndex = 0;
        }

        UpdateSuggestionHighlight();

        if (_selectedSuggestionIndex >= 0 && _selectedSuggestionIndex < _suggestionLabels.Count)
        {
            var label = _suggestionLabels[_selectedSuggestionIndex];
            var scrollPos = _selectedSuggestionIndex * SuggestionItemHeight;
            _autocompleteScroll!.ScrollVertical = scrollPos;
        }
    }

    private void UpdateSuggestionHighlight()
    {
        for (int i = 0; i < _suggestionLabels.Count; i++)
        {
            var label = _suggestionLabels[i];
            var styleBox = new StyleBoxFlat();

            if (i == _selectedSuggestionIndex)
            {
                styleBox.BgColor = new Color(0.3f, 0.5f, 0.7f, 1f);
                label.Modulate = new Color(1f, 1f, 1f);
            }
            else
            {
                styleBox.BgColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                label.Modulate = new Color(0.9f, 0.9f, 0.9f);
            }

            label.AddThemeStyleboxOverride("normal", styleBox);
        }
    }

    private void AcceptAutocomplete()
    {
        if (_currentSuggestions.Count == 0)
        {
            return;
        }

        var index = _selectedSuggestionIndex >= 0 ? _selectedSuggestionIndex : 0;
        if (index < _currentSuggestions.Count)
        {
            _inputField!.Text = _currentSuggestions[index].Text;
            _inputField!.CaretColumn = _inputField!.Text.Length;
        }

        HideAutocomplete();
    }

    public override void OnModuleEnabled()
    {
        base.OnModuleEnabled();
        _inputField!.CallDeferred("grab_focus");
    }

    public override void OnModuleDisabled()
    {
        base.OnModuleDisabled();
        HideAutocomplete();
    }

    public void PrintHelp()
    {
        var commands = _registry!.GetAllCommands();
        var categories = _registry!.GetCommandsByCategory();

        AppendOutput("[color=yellow]Available Commands:[/color]");

        foreach (var category in categories)
        {
            AppendOutput($"\n[color=cyan]{category.Key}:[/color]");
            foreach (var cmd in category)
            {
                AppendOutput($"  {cmd.Name} - {cmd.Description}");
            }
        }
    }

    public void ShowHistory()
    {
        var history = _autocompleteEngine!.GetHistory().ToList();
        if (history.Count == 0)
        {
            AppendOutput("No command history.");
            return;
        }

        AppendOutput("[color=yellow]Command History:[/color]");
        for (int i = 0; i < history.Count; i++)
        {
            AppendOutput($"  {i + 1}. {history[i]}");
        }
    }

    public CommandRegistry? GetRegistry()
    {
        return _registry;
    }
}
#endif
