using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

/// <summary>
/// EWin 的摘要描述
/// </summary>
public static class Pay
{
    public static string SharedFolder = System.Configuration.ConfigurationManager.AppSettings["SharedFolder"];
    public static string DirSplit = "\\";
    public static string DBConnStr = System.Configuration.ConfigurationManager.ConnectionStrings["DBConnStr"].ConnectionString;
    public static DateTime DateTimeNull = Convert.ToDateTime("1900/1/1");
    public static bool IsTestSite = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsTestSite"]);
    public static string ProxyServerUrl = System.Configuration.ConfigurationManager.AppSettings["ProxyServerUrl"];
    public static string ProxyServerUrl2 = System.Configuration.ConfigurationManager.AppSettings["ProxyServerUrl"];
    public static string ProviderSettingPath = System.Configuration.ConfigurationManager.AppSettings["ProviderSettingFolder"];
    public static string WebRedisConnStr = System.Configuration.ConfigurationManager.AppSettings["WebRedisConnStr"];
    public static string GPayBackendKey = System.Configuration.ConfigurationManager.AppSettings["GPayBackendKey"];
    public static string GPayMobile = System.Configuration.ConfigurationManager.AppSettings["GPayMobile"];
    public static string Token = System.Configuration.ConfigurationManager.AppSettings["Token"];
    public static string GPayBackendaApiUrl = System.Configuration.ConfigurationManager.AppSettings["GPayBackendaApiUrl"];
    private static StackExchange.Redis.ConnectionMultiplexer RedisClient = null;

    public static string GetJValue(Newtonsoft.Json.Linq.JObject o, string FieldName, string DefaultValue = null) {
        string RetValue = DefaultValue;

        if (o != null) {
            Newtonsoft.Json.Linq.JToken T;

            T = o[FieldName];
            if (T != null) {
                RetValue = T.ToString();
            }
        }

        return RetValue;
    }

    public static StackExchange.Redis.IDatabase GetRedisClient(int db = -1) {
        StackExchange.Redis.IDatabase RetValue;

        RedisPrepare();

        if (db == -1) {
            RetValue = RedisClient.GetDatabase();
        } else {
            RetValue = RedisClient.GetDatabase(db);
        }

        return RetValue;
    }

    private static void RedisPrepare() {
        if (RedisClient == null) {
            RedisClient = StackExchange.Redis.ConnectionMultiplexer.Connect(WebRedisConnStr);
        }
    }
}


