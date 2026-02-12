namespace GameManager.EnumTypes
{
	/// <summary>
	/// Type of agent-controlled unit
	/// </summary>
	public enum UnitType
    {
		/// <summary>
		/// MINE - a mine to collect gold
		/// </summary>
		MINE,
		/// <summary>
		/// PEON - a unit to gather resources or build things
		/// </summary>
		WORKER,
        /// <summary>
        /// SOLDIER - an attack unit
        /// </summary>
        SOLDIER,
		/// <summary>
		/// ARCHER - a ranged attack unit
		/// </summary>
		ARCHER,
		/// <summary>
		/// BASE - a unit to return collected resources or
		/// train PEONs
		/// </summary>
		BASE,
        /// <summary>
        /// BARRACKS - a unit to train SOLDIERs
        /// </summary>
        BARRACKS,
        /// <summary>
        /// REFINERY - a bonus to resource collection and storage
        /// </summary>
        REFINERY,
    }
}
