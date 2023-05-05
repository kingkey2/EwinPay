<%@Page Language="VB"%>
<%
    Dim DestURL As String

    Response.Clear
    Response.Expires=0

    DestURL = CodingControl.GetQueryString

    If DestURL=String.Empty Then
        DestURL = "/"
    End IF
%>
<html>

<head>
<meta http-equiv="Content-Type" content="text/html; charset=utf-8">
<meta http-equiv="Content-Language" content="zh-cn">
<meta http-equiv="Refresh" content="0; URL=<%=DestURL%>">
</head>
<body>
<p align="center"><font size="2">
Please wait...
</p>
<script language="javascript">
window.location.href="<%=DestURL %>";
</script>
</body>
</html>