using System.Web.Http;
using System.Linq;

public partial class WebAPIConfig
{
    public static void Register(HttpConfiguration config)
    {
        // Web API 設定和服務
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        // Web API 路由
        config.MapHttpAttributeRoutes();

        //config.Routes.MapHttpRoute(
        //    name: "DefaultApi",
        //    routeTemplate: "api/{controller}/{id}",
        //    defaults: new { id = RouteParameter.Optional }
        //);

        config.Routes.MapHttpRoute(
            name: "ActionApi",
            routeTemplate: "api/{controller}/{action}"
        );


        //var appXmlType = config.Formatters.XmlFormatter.SupportedMediaTypes.FirstOrDefault(t => t.MediaType == "application/xml" || t.MediaType == "text/html");
        //config.Formatters.XmlFormatter.SupportedMediaTypes.Remove(appXmlType);
        config.Formatters.XmlFormatter.SupportedMediaTypes.Add(new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data"));
    }
}