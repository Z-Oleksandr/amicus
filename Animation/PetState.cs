namespace AMICUS.Animation
{
    /// <summary>
    /// Defines the different behavioral states the pet can be in
    /// </summary>
    public enum PetState
    {
        Idle,           // Standing/sitting still
        Walking,        // Moving around
        Chasing,        // Chasing the mouse cursor
        Attacking,      // Attacking the mouse cursor
        Sleeping,       // Sleeping with z's
        Playing,        // Playing animation
        Eating          // Eating animation
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
