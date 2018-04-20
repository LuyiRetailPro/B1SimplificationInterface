using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B1SimplificationInterface
{
    class VoucherReturnController
    {

        Settings settings;
        RproDBHandler rproDBHandler;
        MsSqlDBHandler msSqlDBHandler;
        public static int error;
        MainController.Features feature = MainController.Features.VOU_RETURN;
        public VoucherReturnController(Settings settings, RproDBHandler rproDBHandler, MsSqlDBHandler msSqlDBHandler)
        {
            this.settings = settings;
            this.rproDBHandler = rproDBHandler;
            this.msSqlDBHandler = msSqlDBHandler;
        }

        public void runUpdateVoucherReturns()
        {
            error = 0;
            int vou_return_days = Int32.Parse(settings.getDays(feature));

            string day_limit = DateTime.Now.AddDays((vou_return_days + 1) * -1).Date.ToString(MsSqlDBHandler.DATE_FORMAT);
            string sql = "SELECT VOU_SID AS sid FROM Retailpro_Return WHERE vou_date >= " + day_limit;
            HashSet<string> sids = null;
            try
            {
                sids = msSqlDBHandler.getExistingSIDs(sql, rproDBHandler, feature);
            }
            catch (Exception)
            {
                return;
            }

            string subsidiaryFilter = settings.getSubsidiaries(feature);
            if (!string.IsNullOrWhiteSpace(subsidiaryFilter))
            {
                subsidiaryFilter = " and vou.sbs_no in (" + subsidiaryFilter + ") ";
            }
            Dictionary<String, List<VoucherReturnItem>> vou_items = rproDBHandler.getVoucherReturnItems(sids, vou_return_days, subsidiaryFilter);

            msSqlDBHandler.insertVoucherReturns(vou_items, rproDBHandler);
            string msg = vou_items.Count + " return vouchers fetched and inserted into B1 with " + error + " error(s). ";
            if (error > 0)
            {
                string subject = "Exceptions encountered when processing " + feature.ToString();
                string body = msg;
                new EmailController(settings).sendEmail(subject, body, rproDBHandler, feature);
            }
            rproDBHandler.addLog(MainController.LogType.REPORT, "", "", feature, msg, null);
        }

        private Queue<ZeroCostDocument> filterZeroDocuments(Dictionary<String, List<VoucherDiscrepancyItem>> vou_discrepancies)
        {
            Queue<ZeroCostDocument> zeroCostDocuments = new Queue<ZeroCostDocument>();
            HashSet<string> zeroCostSIDs = new HashSet<string>();
            foreach (KeyValuePair<string, List<VoucherDiscrepancyItem>> entry in vou_discrepancies)
            {
                string sid = entry.Key;
                List<VoucherDiscrepancyItem> items = entry.Value;
                bool hasZeroCost = false;
                foreach (VoucherDiscrepancyItem item in items)
                {
                    if (item.hasZeroCost())
                    {
                        hasZeroCost = true;
                        ZeroCostDocument doc = new ZeroCostDocument();
                        doc.alu = item.alu;
                        doc.doc_sid = item.vou_sid;
                        doc.feature = feature;
                        zeroCostDocuments.Enqueue(doc);
                    }
                }
                if (hasZeroCost)
                {
                    zeroCostSIDs.Add(sid);
                }
            }
            foreach (string sid in zeroCostSIDs)
            {
                vou_discrepancies.Remove(sid);
            }
            return zeroCostDocuments;
        }
    }

    public class VoucherReturnItem
    {
        public string vou_sid;
        public string vou_no;
        public string vou_date;
        public string storeid;
        public string subsidiary;
        public string cardcode;
        public string qty;
        public string value;
        public string alu;
        public string division;
        public string comments;
        public string vend_code;
        public string reason;
        public bool hasZeroCost()
        {
            return (Double.Parse(value) == 0);
        }
        public string getSign()
        {
            if (qty.Contains("-"))
            {
                return "Minus";
            }
            return "Plus";
        }
        public string getAbsoluteQty()
        {
            return qty.Replace("-", "");
        }
        public string getSignSingle()
        {
            if (qty.Contains("-"))
            {
                return "M";
            }
            return "P";
        }
    }
}