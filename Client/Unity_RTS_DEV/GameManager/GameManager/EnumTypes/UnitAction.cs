namespace GameManager.EnumTypes
{
	/// <summary>
	/// UnitAction - actions Units can take
	/// </summary>
	public enum UnitAction
    {
        /// <summary>
        /// IDLE - unit has nothing to do
        /// </summary>
        IDLE,
        /// <summary>
        /// MOVE - moving to an arbitrary point
        /// </summary>
        MOVE,
        /// <summary>
        /// TRAIN - training a new unit
        /// </summary>
        TRAIN,
        /// <summary>
        /// BUILD - building a new unit
        /// </summary>
        BUILD,
        /// <summary>
        /// GATHER - gathering a resource
        /// </summary>
        GATHER,
        /// <summary>
        /// ATTACK - attacking a unit
        /// </summary>
        ATTACK
    }

	/// <summary>
	/// State machine variables for animation controls
	/// </summary>
	public enum PeasantAnimation
	{
		/// <summary>
		/// Walking
		/// </summary>
		WALK = 0,
		/// <summary>
		/// Carrying wood
		/// </summary>
		WOOD = 1,
		/// <summary>
		/// Carrying gold
		/// </summary>
		GOLD = 2,
		/// <summary>
		/// Using axe
		/// </summary>
		AXE = 3,
		/// <summary>
		/// Dead
		/// </summary>
		DEAD = 4,
		/// <summary>
		/// Idle
		/// </summary>
		IDLE = 5
	}

	/// <summary>
	/// Speed for animation controls
	/// </summary>
	public enum PeasantSpeed
	{
		/// <summary>
		/// Idle speed (not moving)
		/// </summary>
		IDLE = 0,
		/// <summary>
		/// Walking speed
		/// </summary>
		WALK = 5
	}
}
