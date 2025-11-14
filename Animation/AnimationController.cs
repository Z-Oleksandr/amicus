using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace AMICUS.Animation
{
    /// <summary>
    /// Controls pet animations and state transitions
    /// </summary>
    public class AnimationController
    {
        private SpriteManager _spriteManager;
        private PetState _currentState;
        private PetDirection _currentDirection;

        private List<CroppedBitmap> _currentFrames;
        private int _currentFrameIndex;
        private double _frameTime;
        private double _frameDelay; // Time between frames in seconds

        public PetState CurrentState => _currentState;
        public PetDirection CurrentDirection => _currentDirection;

        public AnimationController()
        {
            _spriteManager = new SpriteManager();
            _currentState = PetState.Idle;
            _currentDirection = PetDirection.Right;
            _currentFrameIndex = 0;
            _frameTime = 0;
            _frameDelay = 0.15; // 150ms between frames (approx 6.67 FPS for smooth animation)

            // Load initial animation
            ChangeState(PetState.Idle);
        }

        /// <summary>
        /// Updates the animation based on elapsed time
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds</param>
        public void Update(double deltaTime)
        {
            _frameTime += deltaTime;

            if (_frameTime >= _frameDelay)
            {
                _frameTime -= _frameDelay;
                _currentFrameIndex = (_currentFrameIndex + 1) % _currentFrames.Count;
            }
        }

        /// <summary>
        /// Gets the current frame to display
        /// </summary>
        public CroppedBitmap GetCurrentFrame()
        {
            if (_currentFrames == null || _currentFrames.Count == 0)
                return null;

            return _currentFrames[_currentFrameIndex];
        }

        /// <summary>
        /// Changes the pet's state and loads appropriate animation
        /// </summary>
        public void ChangeState(PetState newState)
        {
            if (_currentState == newState)
                return;

            _currentState = newState;
            _currentFrameIndex = 0;
            _frameTime = 0;

            // Load appropriate animation frames based on state
            _currentFrames = newState switch
            {
                PetState.Idle => _spriteManager.GetIdleFrames(),
                PetState.Walking => _spriteManager.GetRunningFrames(),
                PetState.Sleeping => _spriteManager.GetSleepingFrames(),
                PetState.Playing => _spriteManager.GetExcitedFrames(),
                PetState.Eating => _spriteManager.GetHappyFrames(),
                _ => _spriteManager.GetIdleFrames()
            };
        }

        /// <summary>
        /// Changes the direction the pet is facing
        /// </summary>
        public void ChangeDirection(PetDirection newDirection)
        {
            _currentDirection = newDirection;
        }

        /// <summary>
        /// Sets the animation speed by adjusting frame delay
        /// </summary>
        /// <param name="fps">Frames per second</param>
        public void SetAnimationSpeed(double fps)
        {
            _frameDelay = 1.0 / fps;
        }
    }
}
