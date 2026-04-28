using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.Resources;
using Structures.GameState;

namespace Constructables.Buildings;

public class BuildingManufacturing
{
    private readonly BuildingConstruction _building;
    
    public ManufacturingState State { get; private set; } = ManufacturingState.Idle;
    public float WorkProgress { get; private set; }
    public float WorkRequired { get; private set; }
    
    // Tracks the inputs that have been reserved/granted for the current cycle
    public Dictionary<string, float> InputsHeld { get; private set; } = new();
    
    // Outputs expected to be produced at the end of the current cycle
    public Dictionary<string, float> ExpectedOutputs { get; private set; } = new();

    public int Priority { get; set; } = 5;

    public BuildingManufacturing(BuildingConstruction building)
    {
        _building = building;
    }

    public void StartCycle(IResourceEndpoint economy, RecipeDefinition recipe, float depositYield, float productionSpeed)
    {
        if (State != ManufacturingState.Idle && State != ManufacturingState.Outputting)
            return;

        WorkRequired = recipe.WorkRequired / productionSpeed;
        WorkProgress = 0f;
        InputsHeld.Clear();
        ExpectedOutputs.Clear();

        // Calculate expected outputs for this cycle (scaling by deposit yield)
        foreach (var output in recipe.OutputResources)
        {
            if (output.Key != "power")
                ExpectedOutputs[output.Key] = output.Value * depositYield;
        }

        // Calculate required inputs for this cycle
        var missingInputs = new Dictionary<string, float>();
        foreach (var input in recipe.InputResources)
        {
            if (input.Key != "power")
            {
                float amountNeeded = input.Value * depositYield;
                if (amountNeeded > 0)
                {
                    missingInputs[input.Key] = amountNeeded;
                }
            }
        }

        if (missingInputs.Count > 0)
        {
            State = ManufacturingState.WaitingForInputs;
            economy.EnqueueResourceRequest(new ResourceRequest(_building, missingInputs, Priority, Time.GetUnixTimeFromSystem()));
        }
        else
        {
            State = ManufacturingState.Manufacturing;
        }
    }

    public void DeliverResource(string resourceId, float amount)
    {
        if (State != ManufacturingState.WaitingForInputs) return;

        if (InputsHeld.ContainsKey(resourceId))
            InputsHeld[resourceId] += amount;
        else
            InputsHeld[resourceId] = amount;
    }

    public void SetState(ManufacturingState newState)
    {
        State = newState;
    }

    public void TickWork(float delta, IResourceEndpoint economy)
    {
        if (State != ManufacturingState.Manufacturing)
            return;

        WorkProgress += delta;

        if (WorkProgress >= WorkRequired)
        {
            FinishCycle(economy);
        }
    }

    private void FinishCycle(IResourceEndpoint economy)
    {
        State = ManufacturingState.Outputting;
        
        // Deposit outputs to economy
        foreach (var output in ExpectedOutputs)
        {
            economy.DepositResource(output.Key, output.Value);
        }

        InputsHeld.Clear();
        ExpectedOutputs.Clear();
        WorkProgress = 0f;
        State = ManufacturingState.Idle; // Ready for next cycle
    }
}
