<%@ Page Language="C#" AutoEventWireup="true" CodeFile="MainFrame.aspx.cs" Inherits="MainFrame" %>


<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title></title>
    <style>
        .bgcolor{
           
        }
        .x-tab-bar-default {
            background-color: #1e4262;
        }

        .x-panel-header-default-framed-top {
            background-color: #1e4262;
        }

        .x-panel-header-default-framed {
            border: 2px solid #1e4262;
        }
        /*.x-tab.x-tab-active.x-tab-default{
            background-color: #B8860B;
        }*/
        .x-tab-inner.x-tab-inner-default {
            /*//font-size:1.08em*/
        }

        .x-panel.x-tabpanel-child.x-panel-default.x-closable.x-panel-closable.x-panel-default-closable {
            padding: 5px 5px 0 5px
        }
    </style>


</head>
<script language="javascript">
    function callPanelScript(frameName, fnName) {
        var panel = Ext.getCmp(frameName);

        if (panel) {
            if (panel.iframe) {
                panel.iframe.dom.contentWindow.eval(fnName);
            }
        }
    }

    function closeTab(Id) {
        var tabPanel = Ext.getCmp("MainTabPanel");

        tabPanel.remove(Id, true);
    }

    function closeActiveTab() {
        var tabPanel = Ext.getCmp("MainTabPanel");
        var tab = tabPanel.getActiveTab();

        tabPanel.remove(tab.id, true);
    }

    function reloadTab(Id) {
        var tab = Ext.getCmp(Id);

        tab.reload();
    }

    function reloadTabToURL(Id, Url) {
        var tab = Ext.getCmp(Id);


        tab.loader.url = Url;
        //tab.loader.load();
        tab.loader.load();
        //tab.reload();
    }

    function reloadActiveTab() {
        var tabPanel = Ext.getCmp("MainTabPanel");
        var tab = tabPanel.getActiveTab();

        tab.reload();
    }

    function reloadActiveTabToURL(Url) {
        var tabPanel = Ext.getCmp("MainTabPanel");
        var tab = tabPanel.getActiveTab();

        tab.loader.url = Url;
        tab.loader.load();


        //tab.reload();
    }

    function playSound(Url) {
        var oSound;

        stopSound();

        oSound = document.createElement("embed");
        oSound.id = "_AudioEmbedded";
        oSound.setAttribute("src", Url);
        oSound.setAttribute("hidden", true);
        oSound.setAttribute("autostart", true);
        document.body.appendChild(oSound);
    }

    function stopSound() {
        var oSound = document.getElementById("_AudioEmbedded");

        if (oSound) {
            document.body.removeChild(oSound);
        }
    }
</script>
<body>
    <form id="form1" runat="server">
        <div>
            <ext:ResourceManager ID="ResourceManager1" runat="server" StateProvider="LocalStorage" />
            <ext:Viewport ID="Viewport1" runat="server" Layout="BorderLayout">
                <MessageBusDirectEvents>
                    <ext:MessageBusDirectEvent Name="<%#ExtControl.MainFrameNameSpace%>" OnEvent="OnMessageBusEvent">
                        <ExtraParams>
                            <ext:Parameter Name="message" Value="data" Mode="Raw" />
                        </ExtraParams>
                    </ext:MessageBusDirectEvent>
                </MessageBusDirectEvents>
                <Items>
                    <ext:Panel runat="server" ID="MenuPanel" meta:resourcekey="MenuPanel" Region="South" Split="false" AutoScroll="false"
                        Collapsible="false">                       
                        <Items>
                            <ext:Component ID="Component1"  Height="10" BaseCls="bgcolor"  runat="server" Flex="1"></ext:Component>
                            <ext:Menu
                                ID="Menu1"
                                runat="server"
                                meta:resourcekey="Menu1"
                                Floating="false"
                                Layout="HBoxLayout"
                                ShowSeparator="false"
                                Height="35"
                                Cls="horizontal-menu">
                                <Defaults>
                                    <ext:Parameter Name="MenuAlign" Value="tl-bl?" Mode="Value" />
                                </Defaults>

                                <Items>

                                    <ext:MenuItem runat="server" ID="miManagement" meta:resourcekey="miManagement" Text="管理" Icon="Folder">
                                        <Menu>
                                            <ext:Menu runat="server">
                                                <Items>
                                                    <ext:MenuItem runat="server" ID="MenuItem3" meta:resourcekey="MenuItem3" Text="工單管理" Icon="Overlays">
                                                        <DirectEvents>
                                                            <Click OnEvent="NodeItem_Click">
                                                                <ExtraParams>
                                                                    <ext:Parameter Name="Text" Value="this.text" Mode="Raw" />
                                                                    <ext:Parameter Name="URL" Value="GameSet_Maint.aspx" />
                                                                </ExtraParams>
                                                            </Click>
                                                        </DirectEvents>
                                                        <Plugins>
                                                            <ext:Badge AlignmentSpec="l-tl" runat="server"></ext:Badge>
                                                        </Plugins>
                                                    </ext:MenuItem>
                                                    <ext:MenuItem runat="server" ID="miRoadMap_Maint" meta:resourcekey="miRoadMap_Maint" Text="賭桌管理" Icon="Table">
                                                        <DirectEvents>
                                                            <Click OnEvent="NodeItem_Click">
                                                                <ExtraParams>
                                                                    <ext:Parameter Name="Text" Value="this.text" Mode="Raw" />
                                                                    <ext:Parameter Name="URL" Value="RoadMap_Maint.aspx" />
                                                                </ExtraParams>
                                                            </Click>
                                                        </DirectEvents>
                                                    </ext:MenuItem>
                                                    <ext:MenuItem ID="MenuItem4" Hidden="true" meta:resourcekey="MenuItem4" runat="server" Text="固定桌賭桌管理" Icon="FolderTable">
                                                        <DirectEvents>
                                                            <Click OnEvent="NodeItem_Click">
                                                                <ExtraParams>
                                                                    <ext:Parameter Name="Text" Value="this.text" Mode="Raw" />
                                                                    <ext:Parameter Name="URL" Value="RoadMap_FixedMaint.aspx" />
                                                                </ExtraParams>
                                                            </Click>
                                                        </DirectEvents>
                                                    </ext:MenuItem>
                                                    <ext:MenuItem runat="server" ID="miAccountingPeriod_Maint" meta:resourcekey="miAccountingPeriod_Maint" Text="結算管理" Icon="Table">
                                                        <DirectEvents>
                                                            <Click OnEvent="NodeItem_Click">
                                                                <ExtraParams>
                                                                    <ext:Parameter Name="Text" Value="this.text" Mode="Raw" />
                                                                    <ext:Parameter Name="URL" Value="AccountingPeriod_Maint.aspx" />
                                                                </ExtraParams>
                                                            </Click>
                                                        </DirectEvents>
                                                    </ext:MenuItem>
                                                </Items>
                                            </ext:Menu>
                                        </Menu>
                                    </ext:MenuItem>
                                    <ext:ToolbarFill runat="server" />
                                    <ext:Button runat="server" meta:resourcekey="btnLogin" Text="登出"
                                        ButtonAlign="Right"
                                        Width="80px"
                                        Icon="ControlPowerBlue"
                                        IconCls="add32"
                                        ID="btnLogout">
                                        <DirectEvents>
                                            <Click OnEvent="Logout_Click">
                                                <EventMask ShowMask="true" Msg="Wait..." MinDelay="500" />
                                            </Click>
                                        </DirectEvents>
                                    </ext:Button>
                                </Items>
                            </ext:Menu>
                            <ext:Component ID="cs"  Height="25" BaseCls="bgcolor"  runat="server" Flex="1"></ext:Component>
                        </Items>
                    </ext:Panel>
                    <ext:Panel runat="server" ID="Panel1" meta:resourcekey="MenuPanel" Region="South" Split="false" AutoScroll="false"
                        Collapsible="false">
                        <Items>
                            
                        </Items>
                    </ext:Panel>
                    <ext:Panel runat="server" Region="Center" Layout="FitLayout">
                        <Items>
                            <ext:TabPanel ID="MainTabPanel" runat="server" Border="false" MinTabWidth="100">
                                <Items>
                                    <ext:Panel ID="Home" runat="server" Title="Home">
                                        <Loader ID="Loader3"
                                            runat="server"
                                            Url="Home.aspx"
                                            Mode="Frame">
                                        </Loader>
                                    </ext:Panel>
                                </Items>
                            </ext:TabPanel>
                        </Items>
                    </ext:Panel>
                </Items>
            </ext:Viewport>
        </div>
    </form>
</body>
</html>

