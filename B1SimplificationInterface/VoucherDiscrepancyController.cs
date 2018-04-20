using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B1SimplificationInterface
{
    class VoucherDiscrepancyController
    {

        Settings settings;
        RproDBHandler rproDBHandler;
        MsSqlDBHandler msSqlDBHandler;
        public static int error;
        public static int zeroCostError;
        MainController.Features feature = MainController.Features.VOU_DISCREPANCY;
        public VoucherDiscrepancyController(Settings settings, RproDBHandler rproDBHandler, MsSqlDBHandler msSqlDBHandler)
        {
            this.settings = settings;
            this.rproDBHandler = rproDBHandler;
            this.msSqlDBHandler = msSqlDBHandler;
        }

        public void runUpdateVoucherDiscrepancies()
        {
            error = 0;
            zeroCostError = 0;
            int vou_disc_days = Int32.Parse(settings.getDays(feature));
            string day_limit = DateTime.Now.AddDays((vou_disc_days + 1) * -1).Date.ToString(MsSqlDBHandler.DATE_FORMAT);

            string sql = "SELECT SUBSTRING(VOU_SID, 4, len(VOU_SID)-4) AS sid FROM Retailpro_Discrepancy WHERE vou_date >= " + day_limit;
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
            Dictionary<String, List<VoucherDiscrepancyItem>> vou_discrepancies = rproDBHandler.getVoucherDiscrepancies(sids, vou_disc_days, subsidiaryFilter);
            Queue<ZeroCostDocument> zeroCostDocuments = filterZeroDocuments(vou_discrepancies);

            msSqlDBHandler.insertVoucherDiscrepancies(vou_discrepancies, rproDBHandler);
            int zeroCostTotal = zeroCostDocuments.Count;
            rproDBHandler.insertZeroCostDocuments(zeroCostDocuments, MainController.Features.VOU_DISCREPANCY);

            string msg = vou_discrepancies.Count + " vouchers with discrepancies fetched and inserted into B1 with " + error + " error(s). ";
            msg += zeroCostTotal + " items with zero cost were inserted with " + zeroCostError + " errors.";

            if (error + zeroCostError > 0)
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
                        doc.feature = MainController.Features.VOU_DISCREPANCY;
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

    public class VoucherDiscrepancyItem
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
        public void calculateAbsoluteTotalCost()
        {
            double totalCost = Double.Parse(getAbsoluteQty()) * Double.Parse(value);
            value = totalCost.ToString("0.00");
        }
    }
}
