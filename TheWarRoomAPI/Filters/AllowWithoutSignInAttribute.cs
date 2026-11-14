namespace TheWarRoomAPI.Filters
{
    // Marks an action that can be called before anyone is signed in (login, team registration).
    // CurrentUserFilter skips these actions.
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class AllowWithoutSignInAttribute : Attribute
    {
    }
}
