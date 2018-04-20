using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace B1SimplificationInterface
{
    public class SlipController
    {
        Settings settings;
        RproDBHandler rproDBHandler;
        MsSqlDBHandler msSqlDBHandler;
        MainController.Features feature = MainController.Features.SLIP;
        public static int error;
        public static int zeroCostError; 
        public SlipController(Settings settings, RproDBHandler rproDBHandler, MsSqlDBHandler msSqlDBHandler)
        {
            this.settings = settings;
            this.rproDBHandler = rproDBHandler;
            this.msSqlDBHandler = msSqlDBHandler;
        }

        public void runUpdateSlips()
        {
            error = 0;
            zeroCostError = 0;
            int slip_days = Int32.Parse(settings.getDays(feature));
            string day_limit = DateTime.Now.AddDays((slip_days+1) * -1).Date.ToString(MsSqlDBHandler.DATE_FORMAT);
            string sql = "SELECT SUBSTRING(SLIP_SID, 4, len(SLIP_SID)-3) AS sid FROM RetailPro_SLIP WHERE SLIP_DATE >= " + day_limit;
            HashSet<string> slipSIDs = null;
            try
            {
                slipSIDs = msSqlDBHandler.getExistingSIDs(sql, rproDBHandler, MainController.Features.SLIP);
            }
            catch (Exception)
            {
                return;
            }
            string subsidiaryFilter = settings.getSubsidiaries(feature);
            if (!string.IsNullOrWhiteSpace(subsidiaryFilter))
            {
                subsidiaryFilter = " and slip.sbs_no in (" + subsidiaryFilter + ") ";
            }
            try
            {
                Queue<Slip> slips = rproDBHandler.getSlips(slipSIDs, slip_days, subsidiaryFilter);
                int slipCount = slips.Count;
                msSqlDBHandler.insertSlips(slips, rproDBHandler);
                sql = "select to_char(item.slip_sid) as doc_sid, alu from slip_item_v item inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 ";
                sql += "inner join slip_v slip on slip.slip_sid = item.slip_sid where nvl(invn.cost, 0) = 0 and slip.modified_date >= trunc(sysdate)- " + slip_days + subsidiaryFilter + " order by item.slip_sid";
                Queue<ZeroCostDocument> zeroCostDocuments = rproDBHandler.getZeroCostDocument(sql, MainController.Features.SLIP);
                int zeroCostTotal = zeroCostDocuments.Count;
                rproDBHandler.insertZeroCostDocuments(zeroCostDocuments, MainController.Features.SLIP);

                string msg = slipCount + " Slips fetched and inserted into B1 with " + error + " error(s). ";
                msg += zeroCostTotal + " items with zero cost were inserted with " + zeroCostError + " errors.";

                if (error + zeroCostError > 0)
                {
                    string subject = "Errors occured when processing " + feature.ToString();
                    string body = msg;
                    new EmailController(settings).sendEmail(subject, body, rproDBHandler, feature);
                }

                rproDBHandler.addLog(MainController.LogType.REPORT, "", "", MainController.Features.SLIP, msg, null);

            }
            catch (Exception e)
            {
                string subject = "Exception occured when sending processing " + feature.ToString();
                string body = "An unexpected exception occured: " + e.ToString();
                new EmailController(settings).sendEmail(subject, body, rproDBHandler, feature);
            }
        }
    }

    public class Slip
    {
        public string slip_sid;
        public string slip_no;
        public string slip_date;
        public string from_store;
        public string from_sbs;
        public string to_store;
        public string to_sbs;
        public string slip_value;
        public string comments;
        public List<string> zeroCostALU = new List<string>();
    }
}
