namespace AMICUS.Animation
{
    /// <summary>
    /// Defines the different behavioral states the pet can be in
    /// </summary>
    public enum PetState
    {
        Idle,           // Standing/sitting still
        Walking,        // Moving around
        Sleeping,       // Sleeping with z's
        Playing,        // Playing animation
        Eating,         // Eating animation
        Chasing,        // Chasing mouse cursor
        Attacking       // Attacking/pouncing
    }

    /// <summary>
    /// Defines the direction the pet is facing
    /// </summary>
    public enum PetDirection
    {
        Left,
        Right
    }
}
