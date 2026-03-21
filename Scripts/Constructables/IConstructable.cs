using Godot;
using Godot.Collections;

public interface IConstructable
{
    [Signal]
    public delegate void OnCompletionEventHandler();

    [Signal]
    public delegate void OnConstructionBlockedEventHandler();

    [Signal]
    public delegate void OnConstructionResumedEventHandler();

    float workRequired { get; set; }
    float workDone { get; set; }
    Dictionary<string, int> requiredResources { get; set; }
    Dictionary<string, int> availableResources { get; set; }

    bool CanConstruct(Dictionary LocationDetails);
    IConstructable StartConstruction(Dictionary LocationDetails);
    bool DefineTemplate(Dictionary templateData);
    bool UpdateConfiguration(Dictionary updateData);
    bool CancelConstruction();
    string GetStatus();
    float GetProgress();
    void UpdateProgress(float delta);
    bool CheckRequiredResourcesAvailable();
    bool CanDemolish();
    bool DemolishConstructable();
    bool CanDestroy();
    bool DestroyConstructable();
}
