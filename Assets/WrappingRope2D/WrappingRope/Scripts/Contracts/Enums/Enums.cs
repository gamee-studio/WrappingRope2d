namespace WrappingRopeLibrary.Enums
{
    public enum Axis
    {
        X, Y, Z
    }


    public enum TexturingMode
    {
        None = 0,
        Stretched = 1,
        TiledFromBackEnd = 2,
        TiledFromFrontEnd = 3
    }


    public enum UVLocation
    {
        AlongU = 0,
        ContraU = 1,
        AlongV = 2,
        ContraV = 3
    }


    public enum AnchoringMode
    {
        None = 0,
        ByFrontEnd = 1,
        ByBackEnd = 2
    }


    public enum Direction
    {
        FrontToBack,
        BackToFront
    }


    public enum BodyType
    {
        FiniteSegments,
        Continuous
    }

}
