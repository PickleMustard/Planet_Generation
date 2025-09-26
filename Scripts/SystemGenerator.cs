using Godot;
using System;

public partial class SystemGenerator : Node
{
    private Node root;

    override public void _Ready()
    {
        root = GetNode("/");
    }

}
