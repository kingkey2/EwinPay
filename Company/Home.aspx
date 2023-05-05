<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Home.aspx.cs" Inherits="Company_Home" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title></title>
    <script>
        function summaryDate(o, v) {
            var date;
            var month;
            var day;
            var retValue;

            if (v != null) {
                date = new Date(v);
                month = date.getMonth() + 1;
                day = date.getDate();
                retValue = month + "/" + day;
            }
            return retValue;
        }

        var gameSetState = function (value) {
            var retValue = "";
            //0=建立/1=進行中/2=暫停/3=完場/4=結算完成/5=取消
            switch (value) {
                //case -1:
                //    retValue = "<font color=#FF0000>尚未建立</font>";
                //    break;
                //case 0:
                //    retValue = "<font color=#000000>建立</font>";
                //    break;
                //case 1:
                //    retValue = "<font color=#0000FF>進行中</font>";
                //    break;
                //case 2:
                //    retValue = "<font color=#00FF00>暫停</font>";
                //    break;
                //case 3:
                //    retValue = "<font color=#000000>完場</font>";
                //    break;
                //case 4:
                //    retValue = "<font color=#000000>結算完成</font>";
                //    break;
                //case 5:
                //    retValue = "<font color=#FF0000>取消</font>";
                //    break;
                case -1:
                    retValue = "<font color=#FF0000><%=EWin.GetLanguage("尚未建立")%></font>";
                break;
            case 0:
                retValue = "<font color=#000000><%=EWin.GetLanguage("建立")%></font>";
                break;
            case 1:
                retValue = "<font color=#0000FF><%=EWin.GetLanguage("進行中")%></font>";
                break;
            case 2:
                retValue = "<font color=#00FF00><%=EWin.GetLanguage("暫停")%></font>";
                break;
            case 3:
                retValue = "<font color=#000000><%=EWin.GetLanguage("完場")%></font>";
                break;
            case 4:
                retValue = "<font color=#000000><%=EWin.GetLanguage("結算完成")%></font>";
                break;
            case 5:
                retValue = "<font color=#FF0000><%=EWin.GetLanguage("取消")%></font>";
                    break;
            }
            return retValue;
        };

        var rewardValue = function (value, meta, record) {
            var v = value;

            if (v >= 0) {
                return "<font color=#0000ff>" + v + "</font>";
            } else {
                return "<font color=#ff0000>" + v + "</font>";
            }
        }

        window.onload = function () {
            App.RewardPanel.setWidth(window.innerWidth - 30);
        };
        Ext.onReady(function () {
            App.RewardPanel.setWidth(window.innerWidth - 30);
        });

        window.onresize = function () {
            App.RewardPanel.setWidth(window.innerWidth - 30);

        }

        var tipsRenderer = function (toolTip, record) {            
            toolTip.setHtml(record.get('RewardValue') + '<%=EWin.GetLanguage("萬")%>');
        };
    </script>
</head>
<body>
    <form id="form1" runat="server">
        <ext:ResourceManager ID="ResourceManager1" runat="server" StateProvider="LocalStorage" />
        <%--        <ext:Viewport 
            runat="server" 
            Layout="BorderLayout"
            Scrollable ="Horizontal"
            AutoScroll ="true"
            >--%>

        <ext:Panel
            ID="RewardPanel"
            meta:resourcekey="RewardPanel"
            Title="總上下數"
            runat="server"
            Frame="true"
            X="5"
            Y="5"
            Height="400"
            Split="true"
            Collapsible="true"
            Layout="FitLayout">
            <TopBar>
                <ext:Toolbar runat="server">
                    <Items>
                    </Items>
                </ext:Toolbar>
            </TopBar>
            <Items>
                <ext:CartesianChart
                    ID="RewardChart"
                    runat="server"
                    meta:resourcekey="RewardChart"
                    InsetPadding="40"
                    StandardTheme="Category1"
                    Height="500"
                    Animate="true">
                    <AnimationConfig Duration="500" Easing="EaseOut" />
                    <Store>
                        <ext:Store
                            runat="server"
                            ID="RewardChartStore"
                            AutoDataBind="true">
                            <Model>
                                <ext:Model ID="RewardChartModel" runat="server">
                                    <Fields>
                                        <ext:ModelField Name="RewardValue" />
                                        <ext:ModelField Name="SummaryDate" />
                                    </Fields>
                                </ext:Model>
                            </Model>
                        </ext:Store>
                    </Store>

                    <Series>
                        <ext:BarSeries
                            XField="SummaryDate"
                            YField="RewardValue">
                            <StyleSpec>
                                <ext:SeriesSprite Opacity="1" MinGapWidth="5" />
                            </StyleSpec>
                            <HighlightConfig>
                                <ext:Sprite
                                    FillStyle="rgba(249, 204, 157, 1.0)"
                                    StrokeStyle="black"
                                    LineWidth="2" />
                            </HighlightConfig>

                            <Tooltip
                                runat="server"
                                TrackMouse="true"
                                StyleSpec="background: gray">
                                <%--<Renderer Handler="toolTip.setHtml(record.get('RewardValue') + '萬');" />--%>
                                <Renderer Fn="tipsRenderer" />
                            </Tooltip>
                            <Label
                                Display="InsideEnd"
                                Field="Data1" />

                        </ext:BarSeries>
                    </Series>
                </ext:CartesianChart>
            </Items>
        </ext:Panel>

        <ext:Panel
            runat="server"
            Layout="HBoxLayout"
            MarginSpec="0 0 0 0"
            Y="5"
            ID="Panel2"
            meta:resourcekey="Panel2"
            Width="1650"
            BodyPadding="10">
            <Items>
                <ext:Panel
                    ID="RankPanel"
                    meta:resourcekey="RankPanel"
                    Title="前十排名"
                    runat="server"
                    MarginSpec="0 20 0 0"
                    Frame="true"
                    Split="true"
                    Width="350"
                    Height="500"
                    BodyPadding="6">
                    <Items>
                        <ext:GridPanel
                            ID="RewardRank"
                            meta:resourcekey="RewardRank"
                            runat="server"
                            Stateful="true"
                            StateID="RewardRank"
                            Region="Center">
                            <Store>
                                <ext:Store
                                    ID="RewardRankStore"
                                    runat="server">
                                    <Model>
                                        <ext:Model ID="Model1" IDProperty="RewardRank" runat="server">
                                            <Fields>
                                                <ext:ModelField Name="RewardRank" />
                                                <ext:ModelField Name="LoginAccount" />
                                                <ext:ModelField Name="SelfRewardValue" />
                                            </Fields>
                                        </ext:Model>
                                    </Model>
                                </ext:Store>
                            </Store>
                            <ColumnModel ID="ColumnModel1" runat="server">
                                <Columns>
                                    <ext:Column ID="Column1" meta:resourcekey="Column1" runat="server" Text="排名" DataIndex="RewardRank"></ext:Column>
                                    <ext:Column ID="Column3" meta:resourcekey="Column3" runat="server" Text="帳號" DataIndex="LoginAccount"></ext:Column>
                                    <ext:Column ID="Column6" meta:resourcekey="Column6" runat="server" Text="上下數" DataIndex="SelfRewardValue">
                                        <Renderer Fn="rewardValue" />
                                    </ext:Column>
                                </Columns>
                            </ColumnModel>
                        </ext:GridPanel>
                    </Items>
                </ext:Panel>
                <ext:Panel
                    ID="GameSetPanel"
                    runat="server"
                    meta:resourcekey="GameSetPanel"
                    Title="未完成工單"
                    Width="750"
                    Height="500"
                    Frame="true"
                    Split="true"
                    MarginSpec="0 20 0 0">
                    <Items>
                        <ext:GridPanel
                            ID="GameSet"
                            meta:resourcekey="GameSet"
                            runat="server"
                            Height="460"
                            Stateful="true">
                            <Store>
                                <ext:Store
                                    ID="GameSetStore"
                                    runat="server">
                                    <Model>
                                        <ext:Model ID="Model2" IDProperty="GameSetID" runat="server">
                                            <Fields>
                                                <ext:ModelField Name="GameSetID" />
                                                <ext:ModelField Name="forCompanyID" />
                                                <ext:ModelField Name="GameSetNumber" />
                                                <ext:ModelField Name="GameSetState" />
                                                <ext:ModelField Name="CustomerType" />
                                                <ext:ModelField Name="UserInitChip" />
                                                <ext:ModelField Name="RadMapNumber" />
                                                <ext:ModelField Name="UserRate" />
                                                <ext:ModelField Name="BuyChipRate" />
                                                <ext:ModelField Name="RewardValue" />
                                                <ext:ModelField Name="BuyChipValue" />
                                                <ext:ModelField Name="ChipValue" />
                                                <ext:ModelField Name="LoginAccount" />
                                                <ext:ModelField Name="forUserAccountID" />
                                            </Fields>
                                        </ext:Model>
                                    </Model>
                                </ext:Store>
                            </Store>
                            <ColumnModel ID="ColumnModel2" runat="server">
                                <Columns>
                                    <ext:Column ID="Column2" meta:resourcekey="Column2" runat="server" Text="單號" DataIndex="GameSetNumber"></ext:Column>
                                    <ext:Column ID="Column10" meta:resourcekey="Column10" runat="server" Text="狀態" DataIndex="GameSetState">
                                        <Renderer Fn="gameSetState" />
                                    </ext:Column>
                                    <ext:Column ID="Column11" meta:resourcekey="Column11" runat="server" Text="本金(萬)" DataIndex="UserInitChip"></ext:Column>
                                    <ext:Column ID="Column12" meta:resourcekey="Column12" runat="server" Text="上下數" DataIndex="RewardValue">
                                        <Renderer Fn="rewardValue" />
                                    </ext:Column>
                                    <ext:Column ID="Column13" meta:resourcekey="Column13" runat="server" Text="轉碼數" DataIndex="BuyChipValue">
                                    </ext:Column>
                                    <ext:Column ID="Column14" meta:resourcekey="Column14" runat="server" Text="加彩" DataIndex="ChipValue"></ext:Column>
                                    <ext:Column ID="Column17" meta:resourcekey="Column17" runat="server" Text="前端下注帳號" DataIndex="LoginAccount"></ext:Column>
                                </Columns>
                            </ColumnModel>
                        </ext:GridPanel>
                    </Items>
                </ext:Panel>

                <ext:FormPanel
                    ID="CompanyDate"
                    Frame="true"
                    runat="server"
                    meta:resourcekey="CompanyDate"
                    Title="公司資料"
                    MarginSpec="0 20 0 0"
                    Resizable="true"
                    ButtonAlign="Center"
                    Layout="FormLayout">
                    <Items>
                        <ext:FieldSet ID="FieldSet1" runat="server"
                            meta:resourcekey="FieldSet1"
                            Title="基本資料"
                            Layout="AnchorLayout">
                            <Items>
                                <ext:FieldContainer
                                    runat="server"
                                    ID="FieldContainer1"
                                    meta:resourcekey="FieldContainer1"
                                    FieldLabel="公司跑馬燈"
                                    AnchorHorizontal="100%"                                    
                                    Layout="HBoxLayout">
                                    <Items>
                                        <ext:Label ID="lbMarqueeText" runat="server" Height="20"  Text=""></ext:Label>
                                    </Items>
                                </ext:FieldContainer>

                            </Items>
                        </ext:FieldSet>
                    </Items>

                </ext:FormPanel>
            </Items>
        </ext:Panel>




        <%--</ext:Viewport>--%>
    </form>
</body>
</html>
