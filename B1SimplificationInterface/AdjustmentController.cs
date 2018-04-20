using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B1SimplificationInterface
{
    class AdjustmentController
    {
        /*
            select distinct item.adj_sid, max(case when invn.cost = 0 then 1 else 0 end) as zeroCostFlag 
            from adj_item_v item inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1
            inner join adjustment_v adj on item.adj_sid = adj.adj_sid
            where adj.created_date >= sysdate - 15
            group by item.adj_sid
            having max(case when invn.cost = 0 then 1 else 0 end) = 0;
            select * from adjustment_v;
            select * from adj_item_v;
            select * from adj_comment_v where comment_no = 1;

            select 
            adj.adj_sid as adj_sid,
            to_char(adj.store_no, '000') as store_code,
            adj.sbs_no as subsidiary,
            sto.glob_store_code as cardcode,
            sum((item.adj_value - item.orig_value) * invn.cost) as totalvalue,
            substr(invn.dcs_code, 0, 3) as division,
            comm.comments as comments
            from adjustment_v adj inner join adj_item_v item on adj.adj_sid = item.adj_sid
            left outer join adj_comment_v comm on adj.adj_sid = comm.adj_sid and comm.comment_no = 1
            inner join store_v sto on adj.store_no = sto.store_no and adj.sbs_no = sto.sbs_no
            inner join inventory_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1
            group by 
            adj.adj_sid,
            to_char(adj.store_no, '000'),
            adj.sbs_no,
            sto.glob_store_code,
            substr(invn.dcs_code, 0, 3),
            comm.comments;
        */
        Settings settings;
        RproDBHandler rproDBHandler;
        MsSqlDBHandler msSqlDBHandler;
        public static int error;
        public static int zeroCostError;
        MainController.Features feature = MainController.Features.ADJUSTMENT;
        public AdjustmentController(Settings settings, RproDBHandler rproDBHandler, MsSqlDBHandler msSqlDBHandler)
        {
            this.settings = settings;
            this.rproDBHandler = rproDBHandler;
            this.msSqlDBHandler = msSqlDBHandler;
            error = 0;
            zeroCostError = 0;
        }

        public void runUpdateAdjustments()
        {
            error = 0;
            zeroCostError = 0;
            int adj_days = Int32.Parse(settings.getDays(feature));
            string day_limit = DateTime.Now.AddDays((adj_days + 1) * -1).Date.ToString(MsSqlDBHandler.DATE_FORMAT);
            string sql = "SELECT SUBSTRING(ADJ_SID, 4, len(ADJ_SID)-3) AS sid FROM RetailPro_ADJUSTMENT WHERE ADJ_DATE >= " + day_limit;
            HashSet<string> adjSIDs = null;
            try
            {
                adjSIDs = msSqlDBHandler.getExistingSIDs(sql, rproDBHandler, feature);
            }
            catch (Exception e)
            {
                return;
            }
           
            string subsidiaryFilter = settings.getSubsidiaries(MainController.Features.ADJUSTMENT);
            if (!string.IsNullOrWhiteSpace(subsidiaryFilter))
            {
                subsidiaryFilter = " and adj.sbs_no in (" + subsidiaryFilter + ") ";
            }
            Dictionary<string, List<Adj_div>> adjustments = rproDBHandler.getAdjustments(adjSIDs, adj_days, subsidiaryFilter);
            msSqlDBHandler.insertAdjustments(adjustments, rproDBHandler);
            // msSqlDBHandler.updateSlipsToDB(slips, rproDBHandler);
            /*
             * with adj as (select distinct item.adj_sid, max(case when invn.cost = 0 then 1 else 0 end) as zeroCostFlag,adj.adj_no as adj_no,
                 to_char(adj.created_date, 'YYYYMMDD') as adj_date, adj.sbs_no as sbs_no, adj.store_no as store_no, adj.adj_reason_name as reason
                 from adj_item_v item inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1
                 inner join adjustment_v adj on item.adj_sid = adj.adj_sid
                 where adj.adj_type = 0 and adj.status = 0 and adj.held = 0 and adj.isreversed = 0 and adj.created_date >= sysdate - 15
                 group by item.adj_sid, adj.adj_no, to_char(adj.created_date, 'YYYYMMDD'), adj.sbs_no , adj.store_no, adj.adj_reason_name
                 having max(case when invn.cost = 0 then 1 else 0 end) = 1)
                 select item.adj_sid as doc_sid, invn.alu as alu from adj_item item inner join adj adj on adj.adj_sid = item.adj_sid 
                 inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 where nvl(invn.cost, 0) = 0
                 */

            sql = "with adj as (select distinct item.adj_sid, max(case when invn.cost = 0 then 1 else 0 end) as zeroCostFlag,adj.adj_no as adj_no, ";
            sql += "to_char(adj.created_date, 'YYYYMMDD') as adj_date, adj.sbs_no as sbs_no, adj.store_no as store_no, adj.adj_reason_name as reason ";
            sql += "from adj_item_v item inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 ";
            sql += "inner join adjustment_v adj on item.adj_sid = adj.adj_sid ";
            sql += "where adj.adj_type = 0 and adj.status = 0 and adj.held = 0 and adj.isreversed = 0 and adj.creating_doc_type!=9 and adj.created_date >= trunc(sysdate) - " + adj_days + subsidiaryFilter + " ";
            sql += "group by item.adj_sid, adj.adj_no, to_char(adj.created_date, 'YYYYMMDD'), adj.sbs_no , adj.store_no, adj.adj_reason_name ";
            sql += "having max(case when invn.cost = 0 then 1 else 0 end) = 1) ";
            sql += "select item.adj_sid as doc_sid, invn.alu as alu from adj_item item inner join adj adj on adj.adj_sid = item.adj_sid ";
            sql += "inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 where nvl(invn.cost, 0) = 0 ";
            Queue<ZeroCostDocument> zeroCostDocuments = rproDBHandler.getZeroCostDocument(sql, MainController.Features.ADJUSTMENT);
            int zeroCostTotal = zeroCostDocuments.Count;
            rproDBHandler.insertZeroCostDocuments(zeroCostDocuments, MainController.Features.ADJUSTMENT);
            string msg = adjustments.Count + " Adjustments fetched and inserted into B1 with " + error + " error(s). ";
            msg += zeroCostTotal + " items with zero cost were inserted with " + zeroCostError + " errors.";
            rproDBHandler.addLog(MainController.LogType.REPORT, "", "", MainController.Features.ADJUSTMENT, msg, null);

          //  if (error > 0)
          //  {
                string subject = "Errors in B1 Interface for " + feature.ToString();
                string body = "There are " + error + " errors when processing " +feature.ToString() + " on " + DateTime.Now.ToString() + ". Please check log for details.";
                new EmailController(settings).sendEmail(subject, body, rproDBHandler, feature);
           // }
        }
    }

    public class Adj_div
    {
        public string adj_sid;
        public string adj_no;
        public string adj_date;
        public string storecode;
        public string subsidiary;
        public string cardcode;
        public string division;
        public string totalvalue;
        public string creating_doc_type;
        public string comments;
        public string getSign()
        {
            if (totalvalue.Contains("-"))
            {
                return "Minus";
            }
            return "Plus";
        }

        public string getB1_status()
        {
            if (Convert.ToDouble(totalvalue) == 0)
            {
                return "Y";
            }
            return "N";
        }

        public string getReason()
        {
            switch (creating_doc_type)
            {
                case "0":
                    return "Uniform";
                case "1":
                    return "PI";
                case "7":
                case "8":
                    return "Manual";
                default:
                    return "";
            }
        }

    }

}
