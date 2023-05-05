<%@ Page Language="C#" %>
<%
    Dictionary<string, string> sendDic = new Dictionary<string, string>();
    sendDic.Add("p_num", "10095");//

    sendDic.Add("txs", "IP2023040110040000242893");//
    sendDic.Add("signature", "18d601cd58c9dd5a36aed622b8772b3b0c29901edae4605d758fe668443a58d10cae111ff3079aee7fa26e69f9947d6bf331bed112c82ccc1813e04b2183fa49");
    string URL = "https://secure.tiger-pay.com/api/settlement_result.php";
    var jsonStr =GatewayCommon.RequestFormDataConentTypeAPI2("https://secure.tiger-pay.com/api/settlement_result.php",sendDic, "IP2023040110040000242893", "TigerPay");


%>

<body>
 <div><%=jsonStr%></div>
</body>