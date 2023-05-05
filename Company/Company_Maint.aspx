<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Company_Maint.aspx.cs" Inherits="Company_Company_Maint" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title></title>
    <style type="text/css">
         .my-grid .x-grid-cell-inner {
            padding-top: 20px;
            padding-bottom: 20px;
            padding-left: 20px;
            padding-right: 20px;
        }
    </style>
    <style type="text/css" media="print">
        @page {
            size: auto; /* auto is the initial value */
            margin: 0; /* this affects the margin in the printer settings */
        }
    </style>
</head>
<script language="javascript">
    var companyState = function (value) {
        var retValue = "";

        switch (value) {
            case 0:
                retValue = "<font color=blue><%=EWin.GetLanguage("正常")%></font>";
                break;
            case 1:
                retValue = "<font color=red><%=EWin.GetLanguage("停用")%></font>";
                break;
        }
        return retValue;
    };

      var companyType = function (value) {
        var retValue = "";

        switch (value) {
            case 0:
                retValue = "<font color=blue><%=EWin.GetLanguage("一般公司")%></font>";
                break;
            case 1:
                retValue = "<font color=blue><%=EWin.GetLanguage("管理公司")%></font>";
                break;
        }
        return retValue;
    };

    var exportData = function (format) {
        App.FormatType.setValue(format);
        var store = App.GridPanel1.store;

        store.submitData(null, { isUpload: true });
    };
</script>
<body>
    <form id="form1" runat="server">
        <div>
            <ext:ResourceManager ID="ResourceManager1" DirectMethodNamespace="Company_Maint" runat="server" StateProvider="LocalStorage" />
            <ext:Store
                ID="Store1"
                runat="server"
                OnReadData="Store1_RefreshData"
                OnSubmitData="Store1_Submit">
                <Model>
                    <ext:Model ID="Model1" IDProperty="CompanyID" runat="server">
                        <Fields>
                            <ext:ModelField Name="CompanyID" />
                            <ext:ModelField Name="CompanyState" />
                            <ext:ModelField Name="CompanyType" />
                            <ext:ModelField Name="CompanyCode" />
                            <ext:ModelField Name="CompanyName" />
                            <ext:ModelField Name="CompanyUserRate" />
                            <ext:ModelField Name="CompanyBuyChipRate" />
                            <ext:ModelField Name="CompanyChipsList" />
                            <ext:ModelField Name="Description" />
                            <ext:ModelField Name="MarqueeText" />
                            <ext:ModelField Name="DomainURL" />
                            <ext:ModelField Name="CompanyCurrentTypeList" />
                            <ext:ModelField Name="DefaultCurrencyType" />
                            <ext:ModelField Name="GuestAccount" />
                            <ext:ModelField Name="CompanyEthWalletAddress" />
                        </Fields>
                    </ext:Model>
                </Model>
            </ext:Store>

            <ext:Hidden ID="FormatType" runat="server" />
            <ext:Viewport ID="Viewport1" runat="server" Layout="BorderLayout">
                <Items>
                    <ext:GridPanel
                        ID="GridPanel1"
                        meta:resourcekey="GridPanel1"
                        runat="server"
                        StoreID="Store1"
                        Stateful="true"
                        StateID="Company_Maint"
                        Region="Center">
                        <ColumnModel ID="ColumnModel1" runat="server">
                            <Columns>
                                <ext:Column ID="Column1" meta:resourcekey="Column1" runat="server" Text="公司代碼" DataIndex="CompanyCode"></ext:Column>
                                <ext:Column ID="Column3" meta:resourcekey="Column3" runat="server" Text="公司狀態" DataIndex="CompanyState">
                                    <Renderer Fn="companyState" />
                                </ext:Column>
                                <ext:Column ID="Column2" meta:resourcekey="Column2" runat="server" Text="公司類別" DataIndex="CompanyType">
                                    <Renderer Fn="companyType" />
                                </ext:Column>
                                <ext:Column ID="Column4" meta:resourcekey="Column4" runat="server" Text="公司名稱" DataIndex="CompanyName"></ext:Column>
                                <ext:Column ID="Column6" meta:resourcekey="Column6" runat="server" Text="公司轉碼率" DataIndex="CompanyBuyChipRate"></ext:Column>
                                <ext:Column ID="Column5" meta:resourcekey="Column5" runat="server" Text="公司佔成率" DataIndex="CompanyUserRate"></ext:Column>
                                <ext:Column ID="Column7" meta:resourcekey="Column7" runat="server" Text="允許顯示籌碼" DataIndex="CompanyChipsList"></ext:Column>
                                <ext:Column ID="Column8" meta:resourcekey="Column8" runat="server" Text="公司描述" DataIndex="Description"></ext:Column>
                                <ext:Column ID="Column9" meta:resourcekey="Column9" runat="server" Text="跑馬燈資訊" DataIndex="MarqueeText"></ext:Column>
                                <ext:Column ID="Column10" meta:resourcekey="Column10" runat="server" Text="對外網址" DataIndex="DomainURL"></ext:Column>
                                <ext:Column ID="Column11" meta:resourcekey="Column11" runat="server" Text="允許使用貨幣" DataIndex="CompanyCurrentTypeList"></ext:Column>
                                <ext:Column ID="Column12" meta:resourcekey="Column12" runat="server" Text="預設使用貨幣," DataIndex="DefaultCurrencyType"></ext:Column>
                                <ext:Column ID="Column13" meta:resourcekey="Column13" runat="server" Text="訪客帳號" DataIndex="GuestAccount"></ext:Column>
                                <ext:Column ID="Column14" meta:resourcekey="Column14" runat="server" Text="乙太坊總錢包地址" DataIndex="CompanyEthWalletAddress"></ext:Column>
                             </Columns>
                        </ColumnModel>
                        <SelectionModel>
                            <ext:RowSelectionModel ID="RowSelectionModel1" runat="server" Mode="Multi" />
                        </SelectionModel>
                        <TopBar>
                            <ext:Toolbar ID="Toolbar1" runat="server">
                                <Items>
                                    <ext:SelectBox
                                        ID="selectCompanyState"
                                        runat="server">
                                        <Items>
                                            <ext:ListItem meta:resourcekey="itemAll" Text="全部" Value="0" />
                                            <ext:ListItem meta:resourcekey="itemEnable" Text="啟用帳戶" Value="2"/>
                                            <ext:ListItem meta:resourcekey="itemDisable" Text="停用帳戶" Value="1" />
                                        </Items>
                                        <DirectEvents>
                                            <Select OnEvent="selectCompanyState_Select"></Select>
                                        </DirectEvents>
                                    </ext:SelectBox>

                                    <ext:Button ID="btnNew" meta:resourcekey="btnNew" runat="server" Icon="ApplicationAdd" Text="新增" >
                                        <DirectEvents>
                                                <Click OnEvent="btnNew_DirectClick"></Click>
                                        </DirectEvents>
                                    </ext:Button>
                                    <ext:ToolbarSeparator />
                                    <ext:Button ID="btnEdit" meta:resourcekey="btnEdit" runat="server" Icon="ApplicationEdit" Text="編輯" >
                                        <DirectEvents>
                                                <Click OnEvent="btnEdit_DirectClick"></Click>
                                        </DirectEvents>
                                    </ext:Button>
                                    <ext:ToolbarSeparator />
                                    <ext:Button ID="btnDelete" meta:resourcekey="btnDelete" runat="server" Icon="ApplicationDelete" Text="停用">
                                       <%-- <Listeners>
                                            <Click Handler="return confirm('確定停用選擇的項目?');"></Click>
                                        </Listeners>
                                         <DirectEvents>
                                                <Click OnEvent="btnDelete_DirectClick"></Click>
                                        </DirectEvents>--%>
                                        <Listeners>
                                            <Click Handler="Company_Maint.DoConfirm();" />
                                        </Listeners>
                                        </ext:Button> 
                                    <ext:ToolbarSeparator />
                                    <ext:ToolbarFill ID="ToolbarFill1" runat="server" />
                                    <ext:Button ID="Button4" runat="server" Text="Print" Icon="Printer" OnClientClick="window.print();" />
                                    <%--<ext:Button ID="Button1" runat="server" Text="To XML" Icon="PageCode">
                                        <Listeners>
                                            <Click Handler="exportData('xml');" />
                                        </Listeners>
                                    </ext:Button>
                                    <ext:Button ID="Button2" runat="server" Text="To Excel" Icon="PageExcel">
                                        <Listeners>
                                            <Click Handler="exportData('xls');" />
                                        </Listeners>
                                    </ext:Button>
                                    <ext:Button ID="Button3" runat="server" Text="To CSV" Icon="PageAttach">
                                        <Listeners>
                                            <Click Handler="exportData('csv');" />
                                        </Listeners>
                                    </ext:Button>--%>
                                </Items>
                            </ext:Toolbar>
                        </TopBar>
                    </ext:GridPanel>
                </Items>
            </ext:Viewport>

        </div>
    </form>
</body>
</html>
