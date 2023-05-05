using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class Company_Home : System.Web.UI.Page
{
    protected override void InitializeCulture()
    {                        
        CodingControl.CheckingLanguage(Request["BackendLang"]);
        base.InitializeCulture();
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        CompanySessionState CSS;
        if (Session["_CompanyLogined"] == null)
            Response.Redirect("RefreshParent.aspx?Login.aspx", true);

        CSS = (CompanySessionState)Session["_CompanyLogined"];


        if (Ext.Net.X.IsAjaxRequest == false)
        {
            RewardChartStore_SetDataBind();
            RewardRankStore_SetDataBind();
            GameSetStore_SetDataBind();
            CompanyInfo_SetDataBind();
        }

    }

    protected void RewardChartStore_SetDataBind()
    {
        System.Data.DataTable DT;
        System.Data.DataTable SummaryDT;
        System.Data.SqlClient.SqlCommand DBCmd;
        CompanySessionState CSS;
        string SS;

        int DayIndex=0;
        DateTime StartDate;
        DateTime EndDate;
        DateTime SimpleDate;
        Ext.Net.CategoryAxis SummaryDateCategory;
        Ext.Net.NumericAxis RewardValueNumeric;

        CSS = (CompanySessionState)Session["_CompanyLogined"];
        string[] CategoryFields = { "SummaryDate" };
        string[] NumericFields = { "RewardValue" };
        if (CSS != null)
        {

            EndDate = DateTime.Now.AddDays(1);
            StartDate = EndDate.AddDays(-31);
            RewardPanel.Title = StartDate.ToShortDateString() + " ~ " + EndDate.ToShortDateString() + " "+ EWin.GetLanguage("總上下數").ToString();
            RewardPanel.TitleAlign = Ext.Net.TitleAlign.Center;

            SummaryDateCategory = new Ext.Net.CategoryAxis()
            {
                Fields = CategoryFields,
                Position = Ext.Net.Position.Bottom,
                Title = EWin.GetLanguage("日期").ToString()
                
            };
            SummaryDateCategory.Renderer.Fn = "summaryDate";
            RewardValueNumeric = new Ext.Net.NumericAxis()
            {
                Fields = NumericFields,
                Position = Ext.Net.Position.Left,
                Grid = true
            };
            var returnLabel = string.Empty;
            returnLabel=string.Format("return label.toFixed(0) + '{0}';", EWin.GetLanguage("萬").ToString());
            RewardValueNumeric.Renderer.Handler = returnLabel;
            //RewardValueNumeric.Renderer.Handler = "return label.toFixed(0) + '萬';";
            
            RewardChart.Axes.Add(SummaryDateCategory);
            RewardChart.Axes.Add(RewardValueNumeric);

            SS = "SELECT " +
                     "ISNULL(SUM(RewardValue),0) AS RewardValue," +
                     "SummaryDate AS SummaryDate " +
                 "FROM SummaryByDate AS S WITH (NOLOCK) " +
                 "LEFT JOIN UserAccount AS U WITH (NOLOCK) " +
                    "ON U.UserAccountID = S.forUserAccountID " +
                 "WHERE " +
                    "S.UserAccountInsideLevel=0 AND S.SummaryDate >= @StartDate AND S.SummaryDate <= @EndDate AND S.forCompanyID=@CompanyID " +
                 "GROUP BY SummaryDate " +
                 "ORDER BY SummaryDate ";           
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@StartDate", System.Data.SqlDbType.DateTime).Value = StartDate;
            DBCmd.Parameters.Add("@EndDate", System.Data.SqlDbType.DateTime).Value = EndDate;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CSS.Company.CompanyID;
            DT = DBAccess.GetDB(EWin.DBConnStr, DBCmd);
            SimpleDate = StartDate.Date;
            SummaryDT = DT.Copy();
            
            //判斷每個日期有無資料，無，加入初始資料(Reward = 0)
            foreach (System.Data.DataRow DR in DT.Rows)
            {
                if (DateTime.Compare(SimpleDate.Date, Convert.ToDateTime(DR["SummaryDate"]).Date) < 0)
                {
                    DayIndex = Convert.ToDateTime(DR["SummaryDate"]).Date.Subtract(SimpleDate.Date).Days+1;
                    
                }
                else if (DateTime.Compare(EndDate.Date, Convert.ToDateTime(DR["SummaryDate"]).Date) > 0)
                {
                    DayIndex = EndDate.Date.Subtract(Convert.ToDateTime(DR["SummaryDate"]).Date).Days ;
                    SimpleDate = Convert.ToDateTime(DR["SummaryDate"]).Date.AddDays(1);
                }

                for (int i =0; i< DayIndex; i++)
                {
                    System.Data.DataRow NewDR = SummaryDT.NewRow();
                    NewDR["RewardValue"] = 0;
                    NewDR["SummaryDate"] = SimpleDate;
                    SummaryDT.Rows.Add(NewDR);
                    SimpleDate = SimpleDate.AddDays(1);
                }

            }

            SummaryDT.DefaultView.Sort = "SummaryDate";

            RewardChartStore.DataSource = SummaryDT.DefaultView;
            RewardChartStore.DataBind();
        }
    }

    protected void RewardRankStore_SetDataBind()
    {
        System.Data.DataTable DT;
        System.Data.SqlClient.SqlCommand DBCmd;
        CompanySessionState CSS;
        string SS;

        DateTime StartDate;
        DateTime EndDate;

        CSS = (CompanySessionState)Session["_CompanyLogined"];

        if(CSS != null)
        {
            EndDate = DateTime.Now.AddDays(1).Date;
            StartDate = EndDate.AddDays(-8).Date;

            SS = "SELECT TOP(10) " +
                    "ROW_NUMBER() OVER(ORDER BY SelfRewardValue DESC) AS RewardRank, " +
                    "* " +
                 "FROM " +
                 "(" +
                    "SELECT  " +
                        "U.LoginAccount ," +
                        "ISNULL(SUM(SelfRewardValue),0) AS SelfRewardValue " +
                    "FROM SummaryByDate AS S WITH (NOLOCK)  " +
                        "LEFT JOIN UserAccount AS U WITH (NOLOCK) " +
                            "ON U.UserAccountID = S.forUserAccountID " +
                    "WHERE " +
                        "S.SummaryDate BETWEEN @StartDate AND @EndDate AND " +
                        "S.forCompanyID=@CompanyID  " +
                    "GROUP BY U.LoginAccount " +
                 ") AS SelfRank ";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.CommandText = SS;
            DBCmd.Parameters.Add("@StartDate", System.Data.SqlDbType.DateTime).Value = StartDate;
            DBCmd.Parameters.Add("@EndDate", System.Data.SqlDbType.DateTime).Value = EndDate;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CSS.Company.CompanyID;
            DT = DBAccess.GetDB(EWin.DBConnStr,DBCmd);

            if (DT != null)
            {
                if(DT.Rows.Count > 0)
                {
                    DT.DefaultView.Sort = "RewardRank";

                    RewardRankStore.DataSource = DT.DefaultView;
                    RewardRankStore.DataBind();
                }
            }

        }
    }

    protected void GameSetStore_SetDataBind()
    {
        System.Data.DataTable DT;
        System.Data.SqlClient.SqlCommand DBCmd;
        CompanySessionState CSS;
        string SS;

        CSS = (CompanySessionState)Session["_CompanyLogined"];

        if (CSS != null)
        {
            SS = "SELECT " +
                    "G.*," +
                    "ISNULL(GR.AddChipValue, 0) As ChipValue, " +
                    "ISNULL(GR.RewardValue, 0) As RewardValue," +
                    "ISNULL(GR.BuyChipValue, 0) As BuyChipValue," +
                    "ISNULL(U.LoginAccount, '') AS LoginAccount " +
                 "FROM GameSetTable AS G WITH (NOLOCK) " +
                    "LEFT JOIN UserAccount AS U WITH (NOLOCK) " +
                        "ON U.UserAccountID=G.forUserAccountID " +
                    "LEFT JOIN GameSetReward As GR With (NOLOCK) " +
                        "On GR.forGameSetID=G.GameSetID " +
                 "WHERE  " +
                    "GameSetState < 3 AND " +
                    "G.forCompanyID=@CompanyID";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.CommandText = SS;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CSS.Company.CompanyID;
            DT = DBAccess.GetDB(EWin.DBConnStr, DBCmd);

            if (DT != null)
            {
                if (DT.Rows.Count > 0)
                {
                    GameSetStore.DataSource = DT;
                    GameSetStore.DataBind();
                }
            }

        }
    }

    protected void CompanyInfo_SetDataBind()
    {
        System.Data.DataTable DT;
        System.Data.SqlClient.SqlCommand DBCmd;
        CompanySessionState CSS;
        string SS;

        CSS = (CompanySessionState)Session["_CompanyLogined"];

        if(CSS != null)
        {
            SS = "SELECT * " +
                 "FROM CompanyTable WITH(NOLOCK) " +
                 "WHERE CompanyID=@CompanyID ";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.CommandText = SS;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value =CSS.Company.CompanyID ;
            DT = DBAccess.GetDB(EWin.DBConnStr,DBCmd);
            if (DT != null)
            {
                if(DT.Rows.Count >0)
                {
                    lbMarqueeText.Text = DT.Rows[0]["MarqueeText"].ToString();
                }
            }           
        }
    }
    
}