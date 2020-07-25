using Ceen.Mvc;

namespace Ceen.PaaS.API
{
    /// <summary>
    /// Marker interface for choosing the API
    /// </summary>
    [Name("api")]
    public interface IAPI : Ceen.Mvc.IControllerPrefix
    {
    }

    /// <summary>
    /// Marker interface for choosing the API v1
    /// </summary>
    [Name("v1")]
    public interface IAPIv1 : IAPI
    {
    }

}