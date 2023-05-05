using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Web;
using System.Web.UI;
using System.Security.Cryptography;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Xml;
using System.Text.RegularExpressions;

public class CodingControl
{

    public static void writeFile(string writeStr)
    {

        //This line executes whether or not the exception occurs.
        var iDir = new System.IO.DirectoryInfo(Pay.SharedFolder + @"\ConsoleFile");
        System.IO.StreamWriter file;

        if (iDir.Exists == false)
        {
            iDir.Create();
        }

        using (file = new System.IO.StreamWriter(iDir + @"\" + DateTime.Now.ToString("yyyyMMdd") + ".txt", true))
        {
            file.WriteLine(writeStr);
        }
    }

    public static string JSEncodeString(string Content)
    {
        if (Content != null)
        {
            return System.Web.HttpUtility.JavaScriptStringEncode(Content);
        }
        else
        {
            return null;
        }
    }

    public static string Base64URLEncode(string SourceString, System.Text.Encoding TextEncoding = null)
    {
        System.Text.Encoding TxtEnc;

        if (TextEncoding == null)
            TxtEnc = System.Text.Encoding.UTF8;
        else
            TxtEnc = TextEncoding;

        return Convert.ToBase64String(TxtEnc.GetBytes(SourceString)).Replace('+', '-').Replace('/', '_');
    }

    public static string Base64URLDecode(string b64String, System.Text.Encoding TextEncoding = null)
    {
        string tmp = b64String.Replace('-', '+').Replace('_', '/');
        string tmp2;
        System.Text.Encoding TxtEnc;

        // 轉換表: '-' -> '+'
        //         '_' -> '/'
        //         c -> c

        if (TextEncoding == null)
            TxtEnc = System.Text.Encoding.UTF8;
        else
            TxtEnc = TextEncoding;

        if ((tmp.Length % 4) == 0)
        {
            tmp2 = tmp;
        }
        else
        {
            tmp2 = tmp + new string('=', 4 - (tmp.Length % 4));
        }

        return TxtEnc.GetString(Convert.FromBase64String(tmp2));
    }

    public static string GetGUID()
    {
        return System.Guid.NewGuid().ToString();
    }

    public static bool GetIsHttps()
    {
        bool RetValue = false;

        if (string.IsNullOrEmpty(HttpContext.Current.Request.Headers["X-Forwarded-Proto"]) == false)
        {
            if (System.Convert.ToString(HttpContext.Current.Request.Headers["X-Forwarded-Proto"]).ToUpper() == "HTTPS")
                RetValue = true;
        }
        else
            RetValue = HttpContext.Current.Request.IsSecureConnection;

        return RetValue;
    }

    public static Tuple<bool, List<Tuple<string, bool>>> CheckXForwardedFor2()
    {

        Tuple<bool, List<Tuple<string, bool>>> RetValue;
        //var gpayIP = new List<string>() { "47.57.7.146", "47.90.122.210", "47.116.48.202", "47.103.41.137", "13.94.39.139", "52.184.37.95", "52.229.204.114", "47.242.46.206", "207.46.156.9"
        //                                 ,"169.56.70.83","10.178.32.23","10.111.65.152","161.202.44.131", "47.104.203.18", "27.102.132.54","172.31.38.85","172.19.254.222","47.242.108.78"};

        var gpayIP = PayDB.GetProxyIPList();
        bool CheckResult = true;
        var CheckIpResults = new List<Tuple<string, bool>>();

        if (string.IsNullOrEmpty(HttpContext.Current.Request.Headers["X-Forwarded-For"]) == false)
        {
            string XForwarder = HttpContext.Current.Request.Headers["X-Forwarded-For"];
            var XForwarderLst = XForwarder.Split(',');

            foreach (var item in XForwarderLst)
            {
                //if (gpayIP.Contains(item.Trim())) {
                if (CheckIPInCDNList(item.Trim()))
                {
                    CheckIpResults.Add(new Tuple<string, bool>(item.Trim(), true));
                }
                else
                {
                    if (!gpayIP.Contains(item.Trim()))
                    {
                        CheckIpResults.Add(new Tuple<string, bool>(item.Trim(), false));
                    }
                    else
                    {
                        CheckIpResults.Add(new Tuple<string, bool>(item.Trim(), true));
                    }
                }
            }

            if (!gpayIP.Contains(HttpContext.Current.Request.UserHostAddress))
            {
                CheckResult = false;
            }
        }
        else
        {
            CheckResult = false;
        }

        RetValue = new Tuple<bool, List<Tuple<string, bool>>>(CheckResult, CheckIpResults);
        return RetValue;

    }

    public static bool CheckXForwardedFor()
    {

        bool RetValue = false;

        //var gpayIP = new List<string>() { "47.57.7.146", "47.90.122.210", "47.116.48.202", "47.103.41.137", "13.94.39.139", "52.184.37.95", "52.229.204.114", "47.242.46.206", "207.46.156.9"
        //                                 ,"169.56.70.83","10.178.32.23","10.111.65.152","161.202.44.131", "47.104.203.18", "27.102.132.54","172.31.38.85","172.19.254.222","47.242.108.78"};
        var gpayIP = PayDB.GetProxyIPList();
        if (string.IsNullOrEmpty(HttpContext.Current.Request.Headers["X-Forwarded-For"]) == false)
        {
            string XForwarder = HttpContext.Current.Request.Headers["X-Forwarded-For"];
            var XForwarderLst = XForwarder.Split(',');
            if (XForwarderLst.Length > 1)
            {
                for (int j = 1; j < XForwarderLst.Count(); j++)
                {
                    if (!gpayIP.Contains(XForwarderLst[j].Trim()))
                    {

                        PayDB.InsertDownOrderTransferLog("XForwarder:" + XForwarder + ";UserHostAddress:" + HttpContext.Current.Request.UserHostAddress, 2, "", "", "55688", true);
                        return RetValue;
                    }
                }
            }

            if (!gpayIP.Contains(HttpContext.Current.Request.UserHostAddress))
            {
                PayDB.InsertDownOrderTransferLog("XForwarder:" + XForwarder + ";UserHostAddress:" + HttpContext.Current.Request.UserHostAddress, 2, "", "", "55688", true);
                return RetValue;
            }
            RetValue = true;
            return RetValue;

        }
        else
        {
            return RetValue;
        }
    }

    public static void UpdateCDNList()
    {
        string json = GetWebTextContent("https://api.cloudflare.com/client/v4/ips");
        JObject IPListJO = JObject.Parse(json);

        JObject IP_LIST = JObject.Parse(IPListJO["result"].ToString());
        string[] ipv4_cidrs = IP_LIST["ipv4_cidrs"].ToString().Replace("[", "").Replace("]", "").Replace("\r\n", "").Replace("\"", "").Replace(" ", "").Split(',');

        foreach (string ipv4 in ipv4_cidrs)
        {
            string[] IP = ipv4.Split('/');
            ClaculateIPRange(IP[0].Trim(), Convert.ToInt32(IP[1].Trim()));
        }
    }

    public static void ClaculateIPRange(string gate, int maskint)
    {
        string BinSubmask = "";
        BinSubmask = BinSubmask.PadLeft(maskint, '1').PadRight(32, '0');
        string SubMaskIP = BinToIP(BinSubmask);

        string[] SplitGate = gate.Split('.');
        string[] BinSplitGate = new string[4];
        BinSplitGate[0] = Convert.ToString(Convert.ToInt32(SplitGate[0]), 2).PadLeft(8, '0');
        BinSplitGate[1] = Convert.ToString(Convert.ToInt32(SplitGate[1]), 2).PadLeft(8, '0');
        BinSplitGate[2] = Convert.ToString(Convert.ToInt32(SplitGate[2]), 2).PadLeft(8, '0');
        BinSplitGate[3] = Convert.ToString(Convert.ToInt32(SplitGate[3]), 2).PadLeft(8, '0');

        string BinBroadcastAddress = (BinSplitGate[0] + BinSplitGate[1] + BinSplitGate[2] + BinSplitGate[3]).Substring(0, maskint).PadRight(32, '1');
        string BroadcastAddress = BinToIP(BinBroadcastAddress);

        long IntIP_to = (long)(uint)IPAddress.NetworkToHostOrder(IPAddress.Parse(BroadcastAddress).GetHashCode());

        RedisCache.CDNList.AddCDNList(gate + "/" + SubMaskIP, IntIP_to);

    }

    public static string BinToIP(string BinStr)
    {
        string[] IP = new string[4];
        IP[0] = Convert.ToInt32(BinStr.Substring(0, 8), 2).ToString();
        IP[1] = Convert.ToInt32(BinStr.Substring(8, 8), 2).ToString();
        IP[2] = Convert.ToInt32(BinStr.Substring(16, 8), 2).ToString();
        IP[3] = Convert.ToInt32(BinStr.Substring(24, 8), 2).ToString();

        return IP[0] + "." + IP[1] + "." + IP[2] + "." + IP[3];
    }

    public static bool CheckIPInCDNList(string IP)
    {
        long IPToInt = (long)(uint)IPAddress.NetworkToHostOrder(IPAddress.Parse(IP).GetHashCode());

        StackExchange.Redis.RedisValue[] GetCDNIP = RedisCache.CDNList.GetCDNList(IPToInt);

        if (GetCDNIP.Length > 0)
        {
            string[] value = GetCDNIP[0].ToString().Split('/');
            string gate = value[0];
            string mask = value[1];
            return CheckIPMaskGateway(mask, gate, IP);
        }
        else
            return false;
    }

    private static bool CheckIPMaskGateway(string mask, string gateway, string ip)
    {
        string[] maskList = mask.Split('.');
        string[] gatewayList = gateway.Split('.');
        string[] ipList = ip.Split('.');
        for (int j = 0; j < maskList.Length; j++)
        {
            if ((int.Parse(gatewayList[j]) & int.Parse(maskList[j])) != (int.Parse(ipList[j]) & int.Parse(maskList[j])))
            {
                return false;
            }
        }

        return true;
    }

    public static string GetUserIP() {
        string RetValue = string.Empty;

        if (string.IsNullOrEmpty(HttpContext.Current.Request.Headers["X-Forwarded-For"]) == false) {
            RetValue = HttpContext.Current.Request.Headers["X-Forwarded-For"];
            if (string.IsNullOrEmpty(RetValue) == false) {
                int tmpInt;

                tmpInt = RetValue.IndexOf(",");
                if (tmpInt != -1) {
                    RetValue = RetValue.Substring(0, tmpInt);
                }
            }
        }
        else {
            RetValue = HttpContext.Current.Request.UserHostAddress;
        }

        IPAddress address;
        if (IPAddress.TryParse(RetValue, out address)) {
            switch (address.AddressFamily) {
                case System.Net.Sockets.AddressFamily.InterNetwork:
                    if (string.IsNullOrEmpty(RetValue) == false) {
                        int tmpIndex;

                        tmpIndex = RetValue.IndexOf(":");
                        if (tmpIndex != -1) {
                            RetValue = RetValue.Substring(0, tmpIndex);
                        }
                    }
                    return RetValue;

                case System.Net.Sockets.AddressFamily.InterNetworkV6:
                    return RetValue;

                default:
                    // umm... yeah... I'm going to need to take your red packet and...
                    break;
            }
        }

        return RetValue;
    }

    public static string RandomPassword(int MaxPasswordChars)
    {
        Random R2 = new Random();

        return RandomPassword(R2, MaxPasswordChars);
    }

    public static string RandomPassword(Random R, int MaxPasswordChars)
    {
        string PasswordString;

        PasswordString = "1234567890ABCDEFGHJKLMNPQRSTUVWXYZ";

        return RandomPassword(R, MaxPasswordChars, PasswordString);
    }

    public static string RandomPassword(Random R, int MaxPasswordChars, string AvailableCharList)
    {
        int I;
        int CharIndex;
        string PasswordString;
        string RetValue;

        RetValue = string.Empty;
        PasswordString = AvailableCharList;
        for (I = 1; I <= MaxPasswordChars; I++)
        {
            CharIndex = R.Next(0, PasswordString.Length - 1);

            RetValue = RetValue + PasswordString.Substring(CharIndex, 1);
        }

        return RetValue;
    }

    public static string GetQueryString()
    {
        string QueryString;
        int QueryStringIndex;

        QueryStringIndex = HttpContext.Current.Request.RawUrl.IndexOf("?");
        QueryString = string.Empty;
        if (QueryStringIndex > 0)
            QueryString = HttpContext.Current.Request.RawUrl.Substring(QueryStringIndex + 1);

        return QueryString;
    }

    public static bool FormSubmit()
    {
        if (HttpContext.Current.Request.HttpMethod.Trim().ToUpper() == "POST")
            return true;
        else
            return false;
    }
    public static void CheckingLanguage(string Lang)
    {
        try
        {
            if (HttpContext.Current.Request["BackendLang"] != null)
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture(Lang);
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(Lang);
            }
            else
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture(GetDefaultLanguage());
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(GetDefaultLanguage());
            }
        }
        catch (Exception ex)
        {

        }
    }
    public static string GetDefaultLanguage()
    {
        // 取得使用者的語言
        // 傳回: 字串, 代表使用者預設的語言集
        string[] LangArr;
        string Temp;
        string[] TempArr;
        string RetValue;

        Temp = HttpContext.Current.Request.ServerVariables["HTTP_ACCEPT_LANGUAGE"];
        TempArr = Temp.Split(';');

        LangArr = TempArr[0].Split(',');

        if (LangArr[0].Trim() == string.Empty)
            RetValue = "en-us";
        else
            RetValue = LangArr[0];

        return RetValue;
    }

    public static byte[] GetWebBinaryContent(string URL)
    {
        byte[] HttpContent;
        System.Net.WebClient HttpClient;

        HttpClient = new System.Net.WebClient();
        HttpContent = HttpClient.DownloadData(URL);

        return HttpContent;
    }

    public static string GetWebTextContent(string URL, string Method = "GET", string SendData = "", string CustomHeader = null, string ContentType = null)
    {
        System.Net.HttpWebRequest HttpClient;
        System.Net.HttpWebResponse HttpResponse;
        System.IO.Stream Stm;
        System.IO.StreamReader SR;
        string RetValue;
        byte[] SendBytes;

        System.Net.ServicePointManager.CertificatePolicy = new TrustAllCertificatePolicy();

        HttpClient = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(URL);
        HttpClient.Method = Method;
        HttpClient.Accept = "*/*";
        HttpClient.UserAgent = "Sender";
        HttpClient.KeepAlive = false;

        if (CustomHeader != null)
        {
            foreach (string EachHead in CustomHeader.Split('\r', '\n'))
            {
                if (string.IsNullOrEmpty(EachHead) == false)
                {
                    string TmpString = EachHead.Replace("\r", "").Replace("\n", "");
                    int tmpIndex = -1;
                    string cmd = null;
                    string value = null;

                    tmpIndex = TmpString.IndexOf(":");
                    if (tmpIndex != -1)
                    {
                        cmd = TmpString.Substring(0, tmpIndex).Trim();
                        value = TmpString.Substring(tmpIndex + 1).Trim();

                        if (string.IsNullOrEmpty(cmd) == false)
                        {
                            HttpClient.Headers.Set(cmd, value);
                        }
                    }
                }
            }
        }

        switch (Method.ToUpper())
        {
            case "POST":
                {
                    SendBytes = System.Text.Encoding.Default.GetBytes(SendData);

                    if (ContentType == null)
                        HttpClient.ContentType = "application/x-www-form-urlencoded";
                    else
                        HttpClient.ContentType = ContentType;

                    HttpClient.ContentLength = SendBytes.Length;
                    HttpClient.GetRequestStream().Write(SendBytes, 0, SendBytes.Length);
                    break;
                }
        }

        try
        {
            HttpResponse = (System.Net.HttpWebResponse)HttpClient.GetResponse();
        }
        catch (System.Net.WebException ex)
        {
            HttpResponse = (System.Net.HttpWebResponse)ex.Response;
        }

        Stm = HttpResponse.GetResponseStream();
        SR = new System.IO.StreamReader(Stm);
        RetValue = SR.ReadToEnd();

        Stm.Close();

        try
        {
            HttpResponse.Close();
        }
        catch (System.Net.WebException ex)
        {
        }

        HttpClient = null;

        return RetValue;
    }

    public static string UserIP()
    {
        // 取得使用者的 IP Address
        return HttpContext.Current.Request.UserHostAddress;
    }

    public static int GetStringLength(string S)
    {
        return System.Text.Encoding.Default.GetByteCount(S);
    }

    public static string XMLSerial(object obj)
    {
        System.Xml.Serialization.XmlSerializer XMLSer;
        System.IO.MemoryStream Stm;
        byte[] XMLArray;
        string RetValue;

        XMLSer = new System.Xml.Serialization.XmlSerializer(obj.GetType());
        Stm = new System.IO.MemoryStream();
        XMLSer.Serialize(Stm, obj);

        Stm.Position = 0;

        XMLArray = new byte[Stm.Length - 1 + 1];
        Stm.Read(XMLArray, 0, XMLArray.Length);
        Stm.Dispose();
        Stm = null;

        RetValue = System.Text.Encoding.UTF8.GetString(XMLArray);

        return RetValue;
    }

    public static object XMLDeserial(string xmlContent, Type objType)
    {
        System.Xml.Serialization.XmlSerializer XMLSer;
        System.IO.MemoryStream Stm;
        byte[] XMLArray;
        object RetValue = null;

        if (xmlContent != string.Empty)
        {
            XMLArray = System.Text.Encoding.UTF8.GetBytes(xmlContent);

            Stm = new System.IO.MemoryStream();
            Stm.Write(XMLArray, 0, XMLArray.Length);
            Stm.Position = 0;
            XMLSer = new System.Xml.Serialization.XmlSerializer(objType);

            RetValue = XMLSer.Deserialize(Stm);

            Stm.Dispose();
            Stm = null;
        }

        return RetValue;
    }

    public static string removeDecimalZero(string strDecimal)
    {
        string returnVal = "";
        if (strDecimal.Contains("."))
        {
            var Decimal0 = strDecimal.Split('.')[1];
            var Decimal1 = strDecimal.Split('.')[1];
            int zeroCount = 0;
            char[] charArray = Decimal0.ToCharArray();
            Array.Reverse(charArray);
            Decimal0 = new string(charArray);
            for (int i = 0; i < Decimal0.Length; i++)
            {
                if (Decimal0[i] == '0')
                {
                    zeroCount++;
                }
                else
                {
                    break;
                }
            }
            Decimal1 = Decimal1.Substring(0, Decimal1.Length - zeroCount);
            if (Decimal1.Length == 0)
            {
                returnVal = strDecimal.Split('.')[0];
            }
            else
            {
                returnVal = strDecimal.Split('.')[0] + "." + Decimal1;
            }

        }
        else
        {
            returnVal = strDecimal;
        }
        return returnVal;
    }

    public static string GetRandomString(int length)
    {
        var str = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var next = new Random();
        var builder = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            builder.Append(str[next.Next(0, str.Length)]);
        }
        return builder.ToString();
    }

    public static string HmacSha512(string clearMessage, string secretKeyString)
    {
        HMACSHA512 HMAC = new HMACSHA512(Encoding.UTF8.GetBytes(secretKeyString));

        String Signature = BitConverter.ToString(HMAC.ComputeHash(Encoding.UTF8.GetBytes(clearMessage))).Replace("-", "");

        //Force the case of the HMAC key to Uppercase
        return Signature.ToLower();
    }

    public static string GetMD5(string DataString, bool Base64Encoding = true)
    {
        return GetMD5(System.Text.Encoding.UTF8.GetBytes(DataString), Base64Encoding);
    }

    public static string GetMD5(byte[] Data, bool Base64Encoding = true)
    {
        System.Security.Cryptography.MD5CryptoServiceProvider MD5Provider = new System.Security.Cryptography.MD5CryptoServiceProvider();
        byte[] hash;
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();

        hash = MD5Provider.ComputeHash(Data);
        MD5Provider = null;

        if (Base64Encoding)
        {
            RetValue.Append(System.Convert.ToBase64String(hash));
        }
        else
        {
            foreach (byte EachByte in hash)
            {
                // => .ToString("x2")
                string ByteStr = EachByte.ToString("x");

                ByteStr = new string('0', 2 - ByteStr.Length) + ByteStr;
                RetValue.Append(ByteStr);
            }
        }


        return RetValue.ToString();
    }

    public static String Sha1Sign(String content, Encoding encode)
    {
        try
        {
            SHA1 sha1 = new SHA1CryptoServiceProvider();//建立SHA1物件
            byte[] bytes_in = encode.GetBytes(content);//將待加密字串轉為byte型別
            byte[] bytes_out = sha1.ComputeHash(bytes_in);//Hash運算
            sha1.Dispose();//釋放當前例項使用的所有資源
            String result = BitConverter.ToString(bytes_out);//將運算結果轉為string型別
            result = result.Replace("-", "").ToUpper();//替換並轉為大寫
            return result;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string GetHMACSHA1Sign(string Data, string Key)
    {
        var encoding = new System.Text.UTF8Encoding();
        byte[] keyByte = encoding.GetBytes(Key);
        byte[] messageBytes = encoding.GetBytes(Data);
        var hmacsha = new System.Security.Cryptography.HMACSHA1(keyByte);
        byte[] hashmessage = hmacsha.ComputeHash(messageBytes);
        return Convert.ToBase64String(hashmessage);
    }

    public static string GetSHA1(string DataString, bool Base64Encoding = true)
    {
        return GetSHA1(System.Text.Encoding.UTF8.GetBytes(DataString), Base64Encoding);
    }

    public static string GetSHA1(byte[] Data, bool Base64Encoding = true)
    {
        System.Security.Cryptography.SHA1CryptoServiceProvider SHA1Provider = new System.Security.Cryptography.SHA1CryptoServiceProvider();
        byte[] hash;
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();

        hash = SHA1Provider.ComputeHash(Data);
        SHA1Provider = null;

        if (Base64Encoding)
        {
            RetValue.Append(System.Convert.ToBase64String(hash));
        }
        else
        {
            foreach (byte EachByte in hash)
            {
                // => .ToString("x2")
                string ByteStr = EachByte.ToString("x");

                ByteStr = new string('0', 2 - ByteStr.Length) + ByteStr;
                RetValue.Append(ByteStr);
            }
        }


        return RetValue.ToString();
    }

    public static string UrlEncodeNissin(string str)
    {
        string urlStr = HttpUtility.UrlEncode(str);
        var urlCode = Regex.Matches(urlStr, "%[a-f0-9]{2}", RegexOptions.Compiled).Cast<Match>().Select(m => m.Value).Distinct();
        foreach (string item in urlCode)
        {
            urlStr = urlStr.Replace(item, item.ToUpper());
        }
        return urlStr;
    }

    public static string UrlEncode(string str)
    {
        StringBuilder builder = new StringBuilder();
        foreach (char c in str)
        {
            if (HttpUtility.UrlEncode(c.ToString(), Encoding.UTF8).Length > 1)
            {
                builder.Append(HttpUtility.UrlEncode(c.ToString(), Encoding.UTF8).ToUpper());
            }
            else
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    public static string HmacSHA384ByXPay(string plaintext, string salt)
    {

        var enc = Encoding.UTF8;
        byte[]
        baText2BeHashed = enc.GetBytes(plaintext),
        baSalt = enc.GetBytes(salt);
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();

        System.Security.Cryptography.HMACSHA384 hasher = new HMACSHA384(baSalt);
        byte[] baHashedText = hasher.ComputeHash(baText2BeHashed);

        foreach (byte EachByte in baHashedText)
        {
            // => .ToString("x2")

            string ByteStr = EachByte.ToString("x");

            ByteStr = new string('0', 2 - ByteStr.Length) + ByteStr;
            RetValue.Append(ByteStr);
        }
        return RetValue.ToString();
    }

    public static string HmacSHA256Bymrgpay(Encoding enc, string plaintext, string salt)
    {
        string result = "";
        enc = enc ?? Encoding.UTF8;
        byte[]
        baText2BeHashed = enc.GetBytes(plaintext),
        baSalt = enc.GetBytes(salt);
        System.Security.Cryptography.HMACSHA256 hasher = new HMACSHA256(baSalt);
        byte[] baHashedText = hasher.ComputeHash(baText2BeHashed);
        /* 
         * HMAC-SHA256
         * https://1024tools.com/hmac 
         */
        #region  1.对上面的"结果A"进行Base64编码           
        //result = string.Join("", baHashedText.ToList().Select(b => b.ToString("x2")).ToArray());
        // result = Convert.ToBase64String(enc.GetBytes(result));
        #endregion
        #region 2.HMAC计算返回原始二进制数据后进行Base64编码
        result = Convert.ToBase64String(baHashedText);
        #endregion
        return result;
    }


    public static string GetHmacSHA256(string SecretKey, string DataString, bool Base64Encoding = true)
    {
        return GetHmacSHA256(System.Text.Encoding.UTF8.GetBytes(SecretKey), System.Text.Encoding.UTF8.GetBytes(DataString), Base64Encoding);
    }

    public static string GetHmacSHA256(byte[] Key, byte[] Data, bool Base64Encoding = true)
    {
        System.Security.Cryptography.HMACSHA256 HmacSHA256Provider = new System.Security.Cryptography.HMACSHA256(Key);
        byte[] hash;
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();

        hash = HmacSHA256Provider.ComputeHash(Data);
        HmacSHA256Provider = null;

        if (Base64Encoding)
        {
            RetValue.Append(System.Convert.ToBase64String(hash));
        }
        else
        {
            foreach (byte EachByte in hash)
            {
                // => .ToString("x2")

                string ByteStr = EachByte.ToString("x");

                ByteStr = new string('0', 2 - ByteStr.Length) + ByteStr;
                RetValue.Append(ByteStr);
            }
        }


        return RetValue.ToString();
    }

    public static string GetSHA256(string DataString, bool Base64Encoding = true)
    {
        return GetSHA256(System.Text.Encoding.UTF8.GetBytes(DataString), Base64Encoding);
    }

    public static string GetSHA256(byte[] Data, bool Base64Encoding = true)
    {
        System.Security.Cryptography.SHA256 SHA256Provider = new System.Security.Cryptography.SHA256CryptoServiceProvider();
        byte[] hash;
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();

        hash = SHA256Provider.ComputeHash(Data);
        SHA256Provider = null;

        if (Base64Encoding)
        {
            RetValue.Append(System.Convert.ToBase64String(hash));
        }
        else
        {
            foreach (byte EachByte in hash)
            {
                // => .ToString("x2")
                string ByteStr = EachByte.ToString("x");

                ByteStr = new string('0', 2 - ByteStr.Length) + ByteStr;
                RetValue.Append(ByteStr);
            }
        }


        return RetValue.ToString();
    }

    public static string SetDESEncrypt(string DataString, string Key, bool Base64Encoding = true)
    {
        //key必須剛好8bytes
        return SetDESEncrypt(System.Text.Encoding.UTF8.GetBytes(DataString), Encoding.ASCII.GetBytes(Key), Base64Encoding);
    }

    public static string SetDESEncrypt(byte[] Data, byte[] Key, bool Base64Encoding = true)
    {
        //key必須剛好8bytes
        DESCryptoServiceProvider DESProvider = new DESCryptoServiceProvider();
        byte[] hash;
        MemoryStream mStream;
        CryptoStream cStream;
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();

        DESProvider.Key = Key;
        DESProvider.IV = Key;

        mStream = new MemoryStream();
        cStream = new CryptoStream(mStream, DESProvider.CreateEncryptor(), CryptoStreamMode.Write);
        cStream.Write(Data, 0, Data.Length);
        cStream.FlushFinalBlock();
        hash = mStream.ToArray();
        DESProvider = null;



        if (Base64Encoding)
        {
            RetValue.Append(System.Convert.ToBase64String(hash));
        }
        else
        {
            foreach (byte EachByte in hash)
            {
                // => .ToString("x2")
                string ByteStr = EachByte.ToString("x");

                ByteStr = new string('0', 2 - ByteStr.Length) + ByteStr;
                RetValue.Append(ByteStr);
            }
        }


        return RetValue.ToString();
    }


    public static string GetDESDecrypt(string DataString, string Key, bool Base64Encoding = true)
    {
        byte[] Data;

        if (Base64Encoding)
        {
            Data = System.Convert.FromBase64String(DataString);
        }
        else
        {
            // x2 => byte[]
            Data = new byte[DataString.Length / 2];
            for (var x = 0; x < DataString.Length / 2; x++)
            {
                var i = (Convert.ToInt32(DataString.Substring(x * 2, 2), 16));
                Data[x] = (byte)i;
            }
        }
        return GetDESDecrypt(Data, Encoding.ASCII.GetBytes(Key));
    }

    public static string GetDESDecrypt(byte[] Data, byte[] Key)
    {
        DESCryptoServiceProvider DESProvider = new DESCryptoServiceProvider();
        byte[] hash;
        MemoryStream mStream;
        CryptoStream cStream;

        DESProvider.Key = Key;
        DESProvider.IV = Key;

        mStream = new MemoryStream();
        cStream = new CryptoStream(mStream, DESProvider.CreateEncryptor(), CryptoStreamMode.Write);
        cStream.Write(Data, 0, Data.Length);
        cStream.FlushFinalBlock();
        hash = mStream.ToArray();
        DESProvider = null;

        return Encoding.UTF8.GetString(hash);
    }


    /// 有密码的AES加密 
    /// </summary>
    /// <param name="text">加密字符</param>
    /// <param name="password">加密的密码</param>
    /// <param name="iv">密钥</param>
    /// <returns></returns>
    public static string AES_Encrypt(string toEncrypt, string key)
    {
        byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
        byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(toEncrypt);

        RijndaelManaged rDel = new RijndaelManaged();
        rDel.Key = keyArray;
        rDel.Mode = CipherMode.ECB;

        ICryptoTransform cTransform = rDel.CreateEncryptor();
        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        return Convert.ToBase64String(resultArray, 0, resultArray.Length);
    }

    /// <summary>
    /// AES解密
    /// </summary>
    /// <param name="text"></param>
    /// <param name="password"></param>
    /// <param name="iv"></param>
    /// <returns></returns>
    public static string AES_Decrypt(string toDecrypt, string key)
    {
        byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
        byte[] toEncryptArray = Convert.FromBase64String(toDecrypt);

        RijndaelManaged rDel = new RijndaelManaged();
        rDel.Key = keyArray;
        rDel.Mode = CipherMode.ECB;
        //rDel.Padding = PaddingMode.PKCS7;

        ICryptoTransform cTransform = rDel.CreateDecryptor();
        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        return UTF8Encoding.UTF8.GetString(resultArray);
    }


    public static string AES_EncryptForYPAY(string toEncrypt, string key)
    {
        byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
        byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(toEncrypt);

        RijndaelManaged rDel = new RijndaelManaged();

        rDel.Mode = CipherMode.CBC;
        rDel.Padding = PaddingMode.PKCS7;

        //rDel.BlockSize = 128;
        //rDel.KeySize = 128;
        rDel.Key = keyArray;
        rDel.IV = keyArray;
        ICryptoTransform cTransform = rDel.CreateEncryptor();

        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        return Convert.ToBase64String(resultArray, 0, resultArray.Length);
    }

    public static string AES_DecryptForYPAY(string toDecrypt, string key)
    {
        byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
        byte[] toEncryptArray = Convert.FromBase64String(toDecrypt);

        RijndaelManaged rDel = new RijndaelManaged();
        rDel.Key = keyArray;
        rDel.Mode = CipherMode.CBC;
        rDel.Padding = PaddingMode.PKCS7;

        rDel.Key = keyArray;
        rDel.IV = keyArray;

        ICryptoTransform cTransform = rDel.CreateDecryptor();
        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        return UTF8Encoding.UTF8.GetString(resultArray);
    }

    public static string Encrypt(string toEncrypt, string key, string iv)
    {
        byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
        byte[] ivArray = UTF8Encoding.UTF8.GetBytes(iv);
        byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(toEncrypt);

        RijndaelManaged rDel = new RijndaelManaged();
        rDel.KeySize = 256;
        rDel.Key = keyArray;
        rDel.IV = ivArray;  // 初始化向量 initialization vector (IV)
        rDel.Mode = CipherMode.CBC; // 密碼分組連結（CBC，Cipher-block chaining）模式
        rDel.Padding = PaddingMode.Zeros;

        ICryptoTransform cTransform = rDel.CreateEncryptor();
        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        return Convert.ToBase64String(resultArray, 0, resultArray.Length);
    }

    public static string Decrypt(string toDecrypt, string key, string iv)
    {
        byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
        byte[] ivArray = UTF8Encoding.UTF8.GetBytes(iv);
        byte[] toEncryptArray = Convert.FromBase64String(toDecrypt);

        RijndaelManaged rDel = new RijndaelManaged();
        rDel.KeySize = 256;
        rDel.Key = keyArray;
        rDel.IV = ivArray;
        rDel.Mode = CipherMode.CBC;
        rDel.Padding = PaddingMode.Zeros;

        ICryptoTransform cTransform = rDel.CreateDecryptor();
        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        return UTF8Encoding.UTF8.GetString(resultArray);
    }

    /// <summary>
    /// AES加密
    /// </summary>
    /// <param name="str">需要加密的字符串</param>
    /// <param name="key">32位密钥</param>
    /// <returns>加密后的字符串</returns>
    public static string AES_EncryptForWPay(string str, string key)
    {
        string Iv = "abcdefghijklmnop";
        Byte[] keyArray = System.Text.Encoding.UTF8.GetBytes(key);
        Byte[] toEncryptArray = System.Text.Encoding.UTF8.GetBytes(str);
        var rijndael = new System.Security.Cryptography.RijndaelManaged();
        rijndael.Key = keyArray;
        rijndael.Mode = System.Security.Cryptography.CipherMode.ECB;
        rijndael.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
        rijndael.IV = System.Text.Encoding.UTF8.GetBytes(Iv);
        System.Security.Cryptography.ICryptoTransform cTransform = rijndael.CreateEncryptor();
        Byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
        return Convert.ToBase64String(resultArray, 0, resultArray.Length);
    }
    /// <summary>
    /// AES解密
    /// </summary>
    /// <param name="str">需要解密的字符串</param>
    /// <param name="key">32位密钥</param>
    /// <returns>解密后的字符串</returns>
    public static string AES_DecryptForWPay(string str, string key)
    {
        string Iv = "abcdefghijklmnop";
        Byte[] keyArray = System.Text.Encoding.UTF8.GetBytes(key);
        Byte[] toEncryptArray = Convert.FromBase64String(str);
        var rijndael = new System.Security.Cryptography.RijndaelManaged();
        rijndael.Key = keyArray;
        rijndael.Mode = System.Security.Cryptography.CipherMode.ECB;
        rijndael.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
        rijndael.IV = System.Text.Encoding.UTF8.GetBytes(Iv);
        System.Security.Cryptography.ICryptoTransform cTransform = rijndael.CreateDecryptor();
        Byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
        return System.Text.Encoding.UTF8.GetString(resultArray);
    }

    public class TrustAllCertificatePolicy : System.Net.ICertificatePolicy
    {
        public bool CheckValidationResult(System.Net.ServicePoint srvPoint, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Net.WebRequest request, int certificateProblem)
        {
            return true;
        }
    }

    public static decimal FormatDecimal(decimal s)
    {
        //decimal iValue;
        //decimal LeftValue;
        //int i = 1;
        //decimal s2;
        //bool IsNegative = false;

        //if (s < 0)
        //    IsNegative = true;

        //s2 = Math.Abs(s);

        //iValue = Math.Floor(s2)/1;

        //LeftValue = s2 % 1;
        //ExtControl.AlertMsg("", "LeftValue="+LeftValue.ToString()+",s2="+ s2.ToString()+ ",iValue=" + iValue.ToString());
        //do
        //{
        //    decimal tmpValue ;
        //    decimal powerNumber = Convert.ToDecimal(Math.Pow(10, i));

        //    tmpValue = (LeftValue * powerNumber % 1);
        //    if (tmpValue == 0)
        //    {
        //        iValue += (LeftValue * powerNumber) * Convert.ToDecimal(Math.Pow(10, -i));

        //        break;
        //    }
        //    else
        //        i += 1;
        //} while (true);

        //if (IsNegative)
        //    return 0 - iValue;
        //else
        //    return iValue;
        return s / 1.000000000000000000000000000000000m;
    }

    public static string EncryptStringFeibao(string plainText, string key, string iv)
    {
        // Instantiate a new Aes object to perform string symmetric encryption
        Aes encryptor = Aes.Create();

        encryptor.Mode = CipherMode.CBC;
        //encryptor.KeySize = 256;
        //encryptor.BlockSize = 128;
        //encryptor.Padding = PaddingMode.Zeros;

        // Set key and IV
        encryptor.Key = Encoding.UTF8.GetBytes(key);
        encryptor.IV = Encoding.UTF8.GetBytes(iv);

        // Instantiate a new MemoryStream object to contain the encrypted bytes
        MemoryStream memoryStream = new MemoryStream();

        // Instantiate a new encryptor from our Aes object
        ICryptoTransform aesEncryptor = encryptor.CreateEncryptor();

        // Instantiate a new CryptoStream object to process the data and write it to the 
        // memory stream
        CryptoStream cryptoStream = new CryptoStream(memoryStream, aesEncryptor, CryptoStreamMode.Write);

        // Convert the plainText string into a byte array
        byte[] plainBytes = Encoding.ASCII.GetBytes(plainText);

        // Encrypt the input plaintext string
        cryptoStream.Write(plainBytes, 0, plainBytes.Length);

        // Complete the encryption process
        cryptoStream.FlushFinalBlock();

        // Convert the encrypted data from a MemoryStream to a byte array
        byte[] cipherBytes = memoryStream.ToArray();

        // Close both the MemoryStream and the CryptoStream
        memoryStream.Close();
        cryptoStream.Close();

        // Convert the encrypted byte array to a base64 encoded string
        string cipherText = Convert.ToBase64String(cipherBytes, 0, cipherBytes.Length);

        // Return the encrypted data as a string
        return cipherText;
    }

    public static string DecryptStringFeibao(string cipherText, string key, string iv)
    {
        // Instantiate a new Aes object to perform string symmetric encryption
        Aes encryptor = Aes.Create();

        encryptor.Mode = CipherMode.CBC;
        //encryptor.KeySize = 256;
        //encryptor.BlockSize = 128;
        //encryptor.Padding = PaddingMode.Zeros;

        // Set key and IV
        encryptor.Key = Encoding.UTF8.GetBytes(key);
        encryptor.IV = Encoding.UTF8.GetBytes(iv);

        // Instantiate a new MemoryStream object to contain the encrypted bytes
        MemoryStream memoryStream = new MemoryStream();

        // Instantiate a new encryptor from our Aes object
        ICryptoTransform aesDecryptor = encryptor.CreateDecryptor();

        // Instantiate a new CryptoStream object to process the data and write it to the 
        // memory stream
        CryptoStream cryptoStream = new CryptoStream(memoryStream, aesDecryptor, CryptoStreamMode.Write);

        // Will contain decrypted plaintext
        string plainText = String.Empty;

        try
        {
            // Convert the ciphertext string into a byte array
            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            // Decrypt the input ciphertext string
            cryptoStream.Write(cipherBytes, 0, cipherBytes.Length);

            // Complete the decryption process
            cryptoStream.FlushFinalBlock();

            // Convert the decrypted data from a MemoryStream to a byte array
            byte[] plainBytes = memoryStream.ToArray();

            // Convert the decrypted byte array to string
            plainText = Encoding.ASCII.GetString(plainBytes, 0, plainBytes.Length);
        }
        finally
        {
            // Close both the MemoryStream and the CryptoStream
            memoryStream.Close();
            cryptoStream.Close();
        }

        // Return the decrypted data as a string
        return plainText;
    }

    public static string DecryptString(string cipherText, string key, string iv)
    {
        // Instantiate a new Aes object to perform string symmetric encryption
        Aes encryptor = Aes.Create();

        encryptor.Mode = CipherMode.CBC;
        //encryptor.KeySize = 256;
        //encryptor.BlockSize = 128;
        //encryptor.Padding = PaddingMode.Zeros;

        // Set key and IV
        encryptor.Key = Encoding.UTF8.GetBytes(key);
        encryptor.IV = Encoding.UTF8.GetBytes(iv);

        // Instantiate a new MemoryStream object to contain the encrypted bytes
        MemoryStream memoryStream = new MemoryStream();

        // Instantiate a new encryptor from our Aes object
        ICryptoTransform aesDecryptor = encryptor.CreateDecryptor();

        // Instantiate a new CryptoStream object to process the data and write it to the 
        // memory stream
        CryptoStream cryptoStream = new CryptoStream(memoryStream, aesDecryptor, CryptoStreamMode.Write);

        // Will contain decrypted plaintext
        string plainText = String.Empty;

        try
        {
            // Convert the ciphertext string into a byte array
            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            // Decrypt the input ciphertext string
            cryptoStream.Write(cipherBytes, 0, cipherBytes.Length);

            // Complete the decryption process
            cryptoStream.FlushFinalBlock();

            // Convert the decrypted data from a MemoryStream to a byte array
            byte[] plainBytes = memoryStream.ToArray();

            // Convert the decrypted byte array to string
            plainText = Encoding.ASCII.GetString(plainBytes, 0, plainBytes.Length);
        }
        finally
        {
            // Close both the MemoryStream and the CryptoStream
            memoryStream.Close();
            cryptoStream.Close();
        }

        // Return the decrypted data as a string
        return plainText;
    }

    public static Dictionary<string, string> AsciiDictionary(Dictionary<string, string> sArray)
    {
        Dictionary<string, string> asciiDic = new Dictionary<string, string>();
        string[] arrKeys = sArray.Keys.ToArray();
        Array.Sort(arrKeys, string.CompareOrdinal);
        foreach (var key in arrKeys)
        {
            string value = sArray[key];
            asciiDic.Add(key, value);
        }
        return asciiDic;
    }

    public static Dictionary<string, object> AsciiDictionary2(Dictionary<string, object> sArray)
    {
        Dictionary<string, object> asciiDic = new Dictionary<string, object>();
        string[] arrKeys = sArray.Keys.ToArray();
        Array.Sort(arrKeys, string.CompareOrdinal);
        foreach (var key in arrKeys)
        {
            object value = sArray[key];
            asciiDic.Add(key, value);
        }
        return asciiDic;
    }


    /// <summary>  
    /// 将c# DateTime时间格式转换为Unix时间戳格式  
    /// </summary>  
    /// <param name="time">时间</param>  
    /// <returns>long</returns>  
    public static long ConvertDateTimeToInt(DateTime time)
    {
        System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1, 0, 0, 0, 0));
        long t = (time.Ticks - startTime.Ticks) / 10000;   //除10000调整为13位      
        return t;
    }

    public static long ConvertDateTimeToUnix(DateTime time)
    {
        System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
        long timeStamp = (long)(time - startTime).TotalSeconds; // 相差秒数
        return timeStamp;
    }

    #region GASH

    #region 產生 ERQC
    // 產生 ERQC      
    #region 使用 Xml String
    /// <summary>
    /// 產生 ERQC
    /// </summary>
    /// <param name="xmlString">交易參數之XML字串</param>
    /// <returns></returns>
    public static String GetERQC(string xmlString, TransactionKey key)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlString);
        return GetERQC(xmlDoc, key);
    }
    #endregion

    #region 使用 XmlDocument
    /// <summary>
    /// 產生 ERQC
    /// </summary>
    /// <param name="xmlDoc">交易參數之 XML</param>
    /// <returns></returns>
    public static String GetERQC(XmlDocument xmlDoc, TransactionKey key)
    {
        String strCID;
        String strCOID;
        String strCUID;
        String strAmt;

        strCID = "";
        strCOID = "";
        strCUID = "";
        strAmt = "";

        #region ERQC Data 組成為 「CID + COID + CUID(3) + AMOUNT(12,2) + PASSWORD」
        // ERQC Data 組成為 「CID + COID + CUID(3) + AMOUNT(12,2) + PASSWORD」
        // ===============================================================================
        /* 修正加密驗證方式
            * 
            * Note:
            * 條件：前期需已完成資料之檢核
            * 1. CID : Content ID
            * 2. COID : Content Order ID
            * 3. CUID : ISO Alpha Currency code (ex: TWD)
            * 4. AMOUNT : 12 碼整數 + 2 碼小數 , 不含小數點
            * 5. PASSWORD : CID 所屬的 MID 密碼
            * 
            */
        // ===============================================================================
        #endregion

        if (xmlDoc.GetElementsByTagName("CID").Count > 0)
            strCID = xmlDoc.GetElementsByTagName("CID").Item(0).InnerText;

        if (xmlDoc.GetElementsByTagName("COID").Count > 0)
            strCOID = xmlDoc.GetElementsByTagName("COID").Item(0).InnerText;

        if (xmlDoc.GetElementsByTagName("CUID").Count > 0)
            strCUID = xmlDoc.GetElementsByTagName("CUID").Item(0).InnerText;

        if (xmlDoc.GetElementsByTagName("AMOUNT").Count > 0)
            strAmt = xmlDoc.GetElementsByTagName("AMOUNT").Item(0).InnerText;

        return GetERQC(strCID, strCOID, strCUID, strAmt, key);
    }
    #endregion

    private static String GetERQC(String strCID, String strCOID, String strCUID, String strAmt, TransactionKey key)
    {
        String strDatas;
        String strKey;
        String strIV;
        String strRtnValue;

        String strPassword;

        strDatas = "{0}{1}{2}{3}{4}";
        strRtnValue = "";

        strPassword = "";

        #region ERQC Data 組成為 「CID + COID + CUID(3) + AMOUNT(12,2) + PASSWORD」
        // ERQC Data 組成為 「CID + COID + CUID(3) + AMOUNT(12,2) + PASSWORD」
        // ===============================================================================
        /* 修正加密驗證方式
            * 
            * Note:
            * 條件：前期需已完成資料之檢核
            * 1. CID : Content ID
            * 2. COID : Content Order ID
            * 3. CUID : ISO Alpha Currency code (ex: TWD)
            * 4. AMOUNT : 12 碼整數 + 2 碼小數 , 不含小數點
            * 5. PASSWORD : CID 所屬的 MID 密碼
            * 
            */
        // ===============================================================================
        #endregion

        // 驗證用的 AMOUNT 需整理成 14 碼
        if (strAmt.Contains("."))
        {
            strAmt = strAmt.Substring(0, strAmt.IndexOf(".")) + ((strAmt.Length - strAmt.IndexOf(".")) > 3 ? strAmt.Substring(strAmt.IndexOf(".") + 1, 2) : strAmt.Substring(strAmt.IndexOf(".") + 1).PadRight(2, '0'));
            strAmt = strAmt.PadLeft(14, '0');
        }
        else
        {
            strAmt = (strAmt + "00").PadLeft(14, '0');
        }

        strPassword = key.Password;
        strKey = key.Key;
        strIV = key.IV;

        strDatas = string.Format(strDatas, strCID, strCOID, strCUID, strAmt, strPassword);

        strRtnValue = MAC(TripleDESEncrypt(strDatas, strKey, strIV));

        return strRtnValue;
    }
    #endregion

    #region 驗證 ERQC
    // 驗證 ERQC
    /// <summary>
    /// 驗證 ERQC
    /// </summary>
    /// <param name="xmlDoc">交易參數之 XML</param>
    /// <returns></returns>
    private static Boolean VerifyERQC(ref XmlDocument xmlDoc, String strERQC, TransactionKey key)
    {
        String strPassword;
        String strGPSERQC;
        String strKey;
        String strIV;

        Boolean bolIsCorrect;

        bolIsCorrect = true;
        strGPSERQC = "";

        strPassword = key.Password;
        strKey = key.Key;
        strIV = key.IV;

        try
        {
            strGPSERQC = GetERQC(xmlDoc, key);
        }
        catch (Exception ex)
        {
            throw ex;
        }

        if (strGPSERQC == "" || strERQC == "" || strGPSERQC != strERQC)
        {
            bolIsCorrect = false;
        }

        return bolIsCorrect;
    }
    #endregion

    #region 產生 ERPC
    // 產生 ERPC
    #region 使用 Xml String
    /// <summary>
    /// 產生 ERPC
    /// </summary>
    /// <param name="xmlString">交易參數之XML字串</param>
    /// <returns></returns>
    private static String GetERPC(string xmlString, TransactionKey key)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlString);
        return GetERPC(xmlDoc, key);
    }
    #endregion

    #region 使用 XmlDocument
    /// <summary>
    /// 產生 ERPC
    /// </summary>
    /// <param name="strString">交易參數之 XML</param>
    /// <returns></returns>
    private static String GetERPC(XmlDocument xmlDoc, TransactionKey key)
    {
        String strCID;
        String strCOID;
        String strRRN;
        String strCUID;
        String strAmt;
        String strRCode;

        strCID = "";
        strCOID = "";
        strRRN = "";
        strCUID = "";
        strAmt = "";
        strRCode = "";

        #region ERPC Data 組成為 「CID + COID + RRN + CUID(3) + AMOUNT(12,2) + RCODE」
        // ERPC Data 組成為 「CID + COID + RRN + CUID(3) + AMOUNT(12,2) + RCODE」
        // ===============================================================================
        /* 修正加密驗證方式
            * 
            * Note:
            * 條件：前期需已完成資料之檢核
            * 1. CID : Content ID
            * 2. COID : Content Order ID
            * 3. RRN : GPS Order ID
            * 4. CUID : ISO Alpha Currency code (ex: TWD)
            * 5. AMOUNT : 12 碼整數 + 2 碼小數 , 不含小數點
            * 6. RCODE : 交易結果代碼
            * 
            */
        // ===============================================================================
        #endregion

        if (xmlDoc.GetElementsByTagName("CID").Count > 0)
            strCID = xmlDoc.GetElementsByTagName("CID").Item(0).InnerText;

        if (xmlDoc.GetElementsByTagName("COID").Count > 0)
            strCOID = xmlDoc.GetElementsByTagName("COID").Item(0).InnerText;

        if (xmlDoc.GetElementsByTagName("RRN").Count > 0)
            strRRN = xmlDoc.GetElementsByTagName("RRN").Item(0).InnerText;

        if (xmlDoc.GetElementsByTagName("CUID").Count > 0)
            strCUID = xmlDoc.GetElementsByTagName("CUID").Item(0).InnerText;

        if (xmlDoc.GetElementsByTagName("AMOUNT").Count > 0)
            strAmt = xmlDoc.GetElementsByTagName("AMOUNT").Item(0).InnerText;

        if (xmlDoc.GetElementsByTagName("RCODE").Count > 0)
            strRCode = xmlDoc.GetElementsByTagName("RCODE").Item(0).InnerText;

        return GetERPC(strCID, strCOID, strRRN, strCUID, strAmt, strRCode, key);
    }
    #endregion

    private static String GetERPC(String strCID, String strCOID, String strRRN, String strCUID, String strAmt, String strRCode, TransactionKey key)
    {
        String strDatas;
        String strKey;
        String strIV;
        String strRtnValue;

        strDatas = "{0}{1}{2}{3}{4}{5}";
        strRtnValue = "";

        if (strCID == "" || strCOID == "" || strCUID == "" || strAmt == "" || strRCode == "") return "";

        #region ERPC Data 組成為 「CID + COID + RRN + CUID(3) + AMOUNT(12,2) + RCODE」
        // ERPC Data 組成為 「CID + COID + RRN + CUID(3) + AMOUNT(12,2) + RCODE」
        // ===============================================================================
        /* 修正加密驗證方式
            * 
            * Note:
            * 條件：前期需已完成資料之檢核
            * 1. CID : Content ID
            * 2. COID : Content Order ID
            * 3. RRN : GPS Order ID
            * 4. CUID : ISO Alpha Currency code (ex: TWD)
            * 5. AMOUNT : 12 碼整數 + 2 碼小數 , 不含小數點
            * 6. RCODE : 交易結果代碼
            * 
            */
        // ===============================================================================
        #endregion

        // 驗證用的 AMOUNT 需整理成 14 碼
        if (strAmt.Contains("."))
        {
            strAmt = strAmt.Substring(0, strAmt.IndexOf(".")) + ((strAmt.Length - strAmt.IndexOf(".")) > 3 ? strAmt.Substring(strAmt.IndexOf(".") + 1, 2) : strAmt.Substring(strAmt.IndexOf(".") + 1).PadRight(2, '0'));
            strAmt = strAmt.PadLeft(14, '0');
        }
        else
        {
            strAmt = (strAmt + "00").PadLeft(14, '0');
        }

        strKey = key.Key;
        strIV = key.IV;

        strDatas = string.Format(strDatas, strCID, strCOID, strRRN, strCUID, strAmt, strRCode);

        strRtnValue = MAC(TripleDESEncrypt(strDatas, strKey, strIV));

        return strRtnValue;
    }
    #endregion

    #region 驗證 ERPC
    // 驗證 ERPC
    public static Boolean VerifyERPC(ref XmlDocument xmlDoc, String strERPC, TransactionKey key)
    {
        String strCID;
        String strGPSERPC;
        String strKey;
        String strIV;

        Boolean bolIsCorrect;

        bolIsCorrect = true;
        strCID = "";
        strGPSERPC = "";

        if (xmlDoc.GetElementsByTagName("CID").Count > 0)
            strCID = xmlDoc.GetElementsByTagName("CID").Item(0).InnerText;

        strKey = key.Key;
        strIV = key.IV;

        try
        {
            strGPSERPC = GetERPC(xmlDoc, key);
        }
        catch (Exception ex)
        {
            throw ex;
        }

        if (strGPSERPC == "" || strERPC == "" || strGPSERPC != strERPC)
        {
            bolIsCorrect = false;
        }

        return bolIsCorrect;
    }
    #endregion

    public static String SpecialTripleDESEncrypt(String data, String key, String iv)
    {
        byte[] byResult = null;

        using (MemoryStream memoryStream = new MemoryStream())
        {
            ICryptoTransform cryptoTransform = null;
            TripleDESCryptoServiceProvider oCrypto = new TripleDESCryptoServiceProvider();

            cryptoTransform = oCrypto.CreateEncryptor(Convert.FromBase64String(key), Convert.FromBase64String(iv));

            using (CryptoStream encStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write))
            {
                using (StreamWriter streamWriter = new StreamWriter(encStream, Encoding.ASCII))
                {
                    // Write the originalValue to the stream.
                    streamWriter.WriteLine(data);
                    // Close the StreamWriter.
                    streamWriter.Close();
                }
                // Close the CryptoStream.
                encStream.Close();
            }
            // Get an array of bytes that represents the memory stream.
            byResult = memoryStream.ToArray();
            // Close the memory stream.
            memoryStream.Close();
        }

        return Convert.ToBase64String(byResult);
    }

    public static String SpecialTripleDESDecrypt(String data, String key, String iv)
    {
        byte[] byResult = null;

        using (MemoryStream memoryStream = new MemoryStream())
        {
            ICryptoTransform cryptoTransform = null;
            TripleDESCryptoServiceProvider oCrypto = new TripleDESCryptoServiceProvider();

            cryptoTransform = oCrypto.CreateDecryptor(Convert.FromBase64String(key), Convert.FromBase64String(iv));

            using (CryptoStream encStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write))
            {
                using (StreamWriter streamWriter = new StreamWriter(encStream, Encoding.ASCII))
                {
                    // Write the originalValue to the stream.
                    streamWriter.WriteLine(data);
                    // Close the StreamWriter.
                    streamWriter.Close();
                }
                // Close the CryptoStream.
                encStream.Close();
            }
            // Get an array of bytes that represents the memory stream.
            byResult = memoryStream.ToArray();
            // Close the memory stream.
            memoryStream.Close();
        }

        return Convert.ToBase64String(byResult);
    }

    public static String TripleDESEncrypt(String strString, String strKey, String strIV)
    {
        TripleDESCryptoServiceProvider crypto = new TripleDESCryptoServiceProvider();
        if (strKey.Length != 32) throw new ArgumentException("TripleDES 金鑰長度必須為 32 個位元。");
        crypto.Key = Convert.FromBase64String(strKey);
        if (strIV.Length != 12) throw new ArgumentException("TripleDES 向量長度必須為 12 個位元。");
        if (strIV.LastIndexOf("=") != 11) throw new ArgumentException("TripleDES 向量格式不正確。");
        crypto.IV = Convert.FromBase64String(strIV);

        byte[] byResult = null;
        // Encrypt a string to bytes.
        using (MemoryStream memoryStream = new MemoryStream())
        {
            ICryptoTransform cryptoTransform = crypto.CreateEncryptor();

            using (CryptoStream encStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write))
            {
                UTF8Encoding oUTF8 = new UTF8Encoding(false);

                using (StreamWriter streamWriter = new StreamWriter(encStream, oUTF8))
                {
                    // Write the originalValue to the stream.
                    streamWriter.Write(strString);
                    // Close the StreamWriter.
                    streamWriter.Close();
                }
                // Close the CryptoStream.
                encStream.Close();
            }
            // Get an array of bytes that represents the memory stream.
            byResult = memoryStream.ToArray();
            // Close the memory stream.
            memoryStream.Close();
        }

        return Convert.ToBase64String(byResult);
    }

    public static String TripleDESDecrypt(String strString, String strKey, String strIV)
    {
        TripleDESCryptoServiceProvider crypto = new TripleDESCryptoServiceProvider();
        if (strKey.Length != 32) throw new ArgumentException("TripleDES 金鑰長度必須為 32 個位元。");
        crypto.Key = Convert.FromBase64String(strKey);
        if (strIV.Length != 12) throw new ArgumentException("TripleDES 向量長度必須為 12 個位元。");
        if (strIV.LastIndexOf("=") != 11) throw new ArgumentException("TripleDES 向量格式不正確。");
        crypto.IV = Convert.FromBase64String(strIV);

        String szResult = null;

        using (MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(strString)))
        {
            ICryptoTransform cryptoTransform = crypto.CreateDecryptor();

            // Create a CryptoStream using the memory stream and the CSP DES key. 
            using (CryptoStream encStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Read))
            {
                UTF8Encoding oUTF8 = new UTF8Encoding(false);

                // Create a StreamReader for reading the stream.
                using (StreamReader streamReader = new StreamReader(encStream, oUTF8))
                {
                    // Read the stream as a string.
                    szResult = streamReader.ReadToEnd();
                    // Close the streams.
                    streamReader.Close();
                }
                // Close the CryptoStream.
                encStream.Close();
            }
            // Close the memory stream.
            memoryStream.Close();
        }

        return szResult;
    }

    public static String StringToMD5(String strString)
    {
        MD5CryptoServiceProvider crypto = new MD5CryptoServiceProvider();

        byte[] byValue = Encoding.UTF8.GetBytes(strString);
        byte[] byHash = crypto.ComputeHash(byValue);

        crypto.Clear();
        return Convert.ToBase64String(byHash);
    }

    public static String StringToSHA1(String strString)
    {
        SHA1CryptoServiceProvider crypto = new SHA1CryptoServiceProvider();

        byte[] byValue = Encoding.UTF8.GetBytes(strString);
        byte[] byHash = crypto.ComputeHash(byValue);

        crypto.Clear();
        return Convert.ToBase64String(byHash);
    }

    public static String MAC(String strString)
    {
        return StringToSHA1(strString);
    }

    public class TransactionKey
    {
        public string Password; //交易密碼
        public string Key;      //交易密鑰1
        public string IV;       //交易密鑰2
    }

    #endregion




}
