<%@Page Language="VB"%>
<%
    Dim DestURL As String

    Response.Clear()
    Response.Expires = 0

    DestURL = CodingControl.GetQueryString()

    If DestURL = String.Empty Then
        DestURL = "/"
    End If
%>
<html>

<head>
<meta http-equiv="Content-Type" content="text/html; charset=utf-8">
<meta http-equiv="Content-Language" content="zh-cn">
</head>
<body>
<p align="center"><font size="2">
Please wait...</font>
</p>
<script language="javascript">
window.top.location.href="<%=DestURL %>";
</script>
</body>
</html>