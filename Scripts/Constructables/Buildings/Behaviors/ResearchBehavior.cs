
namespace Constructables.Buildings.Behaviors;

///<summary>
///On each tick, will add progress to its specified research
///Can require resources before research is progressed
///</summary>
public partial class ResearchBehavior : IBuildingBehavior
{
    private Building? _owner;

    public Building? Owner => _owner;

    public void OnAttach(Building owner)
    {
        throw new System.NotImplementedException();
    }

    public void OnDetach()
    {
        throw new System.NotImplementedException();
    }

    public void OnManufactureTick(float delta, Building owner)
    {
        throw new System.NotImplementedException();
    }

    public void OnRegister()
    {
        throw new System.NotImplementedException();
    }

    public void OnUnregister()
    {
        throw new System.NotImplementedException();
    }
}
