namespace GameManager.Graph
{
	internal interface IBuildable
    {
        //bool Walkable();
        bool IsBuildable();

		void SetBuildable(bool isBuildable);
    }
}
