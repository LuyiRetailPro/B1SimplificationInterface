using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B1SimplificationInterface
{
    class SFSVoucherReturnController
    {
        Settings settings;
        RproDBHandler rproDBHandler;
        MsSqlDBHandler msSqlDBHandler;
        public static int error;
        MainController.Features feature = MainController.Features.SFS_VOU_RETURN;

        public SFSVoucherReturnController(Settings settings, RproDBHandler rproDBHandler, MsSqlDBHandler msSqlDBHandler)
        {
            this.settings = settings;
            this.rproDBHandler = rproDBHandler;
            this.msSqlDBHandler = msSqlDBHandler;
        }

        public void runUpdateSFSVoucherReturns()
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
            Dictionary<String, List<VoucherReturnItem>> vou_items = rproDBHandler.getSFSVoucherReturnItems(sids, vou_return_days, subsidiaryFilter);

            msSqlDBHandler.insertVoucherReturns(vou_items, rproDBHandler);
            string msg = vou_items.Count + " SFS return vouchers fetched and inserted into B1 with " + error + " error(s). ";
            if (error > 0)
            {
                string subject = "Exceptions encountered when processing " + feature.ToString();
                string body = msg;
                new EmailController(settings).sendEmail(subject, body, rproDBHandler, feature);
            }
            rproDBHandler.addLog(MainController.LogType.REPORT, "", "", feature, msg, null);
        }      
    }
   
}
