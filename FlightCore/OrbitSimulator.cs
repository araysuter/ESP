using System;

namespace FlightCore
{
    /// <summary>
    /// A gravity-only fixed-step orbital simulator using velocity Verlet integration.
    /// </summary>
    /// <remarks>
    /// This simulator uses a simplified Earth-centered inertial frame with SI units:
    /// - Position: meters
    /// - Velocity: meters/second  
    /// - Time: seconds (mission time)
    /// 
    /// Integration method: Velocity Verlet
    /// Coordinate convention: Simplified Earth-centered inertial frame (not ICRF)
    /// 
    /// The simulation advances by exactly the configured time step on each Step() call.
    /// </remarks>
    public class OrbitSimulator
    {
        private readonly double _timeStep;
        private Vector3d _position;
        private Vector3d _velocity;
        private double _missionTime;

        /// <summary>
        /// Gets a read-only snapshot of the current state.
        /// </summary>
        public OrbitStateSnapshot State => new OrbitStateSnapshot(_position, _velocity, _missionTime);

        /// <summary>
        /// Creates a new orbit simulator with the specified time step.
        /// </summary>
        /// <param name="timeStep">The positive, finite time step for simulation in seconds.</param>
        /// <exception cref="ArgumentException">Thrown when timeStep is not positive or finite.</exception>
        public OrbitSimulator(double timeStep)
        {
            if (timeStep <= 0.0)
                throw new ArgumentException("Time step must be positive.", nameof(timeStep));
            
            if (double.IsNaN(timeStep) || double.IsInfinity(timeStep))
                throw new ArgumentException("Time step must be finite.", nameof(timeStep));

            _timeStep = timeStep;
        }

        /// <summary>
        /// Initializes the simulator with the specified initial conditions.
        /// </summary>
        /// <param name="initialPosition">The initial position in Earth-centered inertial frame (meters).</param>
        /// <param name="initialVelocity">The initial velocity in Earth-centered inertial frame (m/s).</param>
        /// <param name="initialTime">The initial mission time (seconds).</param>
        public void Initialize(Vector3d initialPosition, Vector3d initialVelocity, double initialTime = 0.0)
        {
            _position = initialPosition;
            _velocity = initialVelocity;
            _missionTime = initialTime;
        }

        /// <summary>
        /// Advances the simulation by exactly one time step.
        /// </summary>
        public void Step()
        {
            // Velocity Verlet integration
            // First half-step for velocity (using current position)
            Vector3d acceleration = Gravity.Acceleration(_position);
            Vector3d halfStepVelocity = _velocity + 0.5 * _timeStep * acceleration;
            
            // Full step for position
            Vector3d newPosition = _position + _timeStep * halfStepVelocity;
            
            // Second half-step for velocity (using new position)
            Vector3d newAcceleration = Gravity.Acceleration(newPosition);
            Vector3d finalVelocity = halfStepVelocity + 0.5 * _timeStep * newAcceleration;
            
            // Advance time
            double newMissionTime = _missionTime + _timeStep;
            
            // Update state only after all calculations succeed
            _position = newPosition;
            _velocity = finalVelocity;
            _missionTime = newMissionTime;
        }

        /// <summary>
        /// Gets the time step used by this simulator.
        /// </summary>
        public double TimeStep => _timeStep;
    }

    /// <summary>
    /// A read-only snapshot of the orbital simulator state.
    /// </summary>
    public readonly struct OrbitStateSnapshot
    {
        /// <summary>
        /// The position in Earth-centered inertial frame (meters).
        /// </summary>
        public readonly Vector3d Position;

        /// <summary>
        /// The velocity in Earth-centered inertial frame (m/s).
        /// </summary>
        public readonly Vector3d Velocity;

        /// <summary>
        /// The current mission time (seconds).
        /// </summary>
        public readonly double MissionTime;

        /// <summary>
        /// Creates a new snapshot with the specified values.
        /// </summary>
        /// <param name="position">The position vector.</param>
        /// <param name="velocity">The velocity vector.</param>
        /// <param name="missionTime">The mission time.</param>
        public OrbitStateSnapshot(Vector3d position, Vector3d velocity, double missionTime)
        {
            Position = position;
            Velocity = velocity;
            MissionTime = missionTime;
        }
    }
}