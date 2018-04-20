using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace B1SimplificationInterface
{
    public class MainController
    {
        public static int storeSyncError;
        //shared resources
        public Settings settings;
        public RproDBHandler rproDBHandler;
        public MsSqlDBHandler msSqlDBHandler;
        //mysqldb handler
        //settings files

        public MainController()
        {
            settings = new Settings();
            rproDBHandler = new RproDBHandler(settings);
            msSqlDBHandler = new MsSqlDBHandler(settings);

        }

        public void runFromArgs(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i].ToUpper();
                try
                {
                    if (arg.Contains("CLEARZEROCOST:"))
                    {
                        arg = arg.Replace("CLEARZEROCOST:", "");
                        try
                        {
                            int days_ago = Int32.Parse(arg);
                            deleteLogs(arg);
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show("Error in command DELETELOG - format should be 'CLEARZEROCOST:10' for deleting log for past 10 days");
                        }
                    }

                    else
                    {
                        MainController.Features feature = getFeatureByName(arg);
                        switch (feature)
                        {
                            case Features.SLIP:
                                runSlips();
                                break;
                            case Features.ADJUSTMENT:
                                runAdjustments();
                                break;
                            case Features.SALE:
                                runInvoices();
                                break;
                            case Features.ITEM_COST:
                                runItemCost();
                                break;
                            case Features.VOU_DISCREPANCY:
                                runVoucherDiscrepancies();
                                break;
                            case Features.VOU_RETURN:
                                runVoucherReturns();
                                break;
                            case Features.SEND_EMAIL:
                                runSendDailyEmail();
                                break;
                        }
                    }

                }catch (Exception ex)
                {
                    MessageBox.Show("Windows command not recognized: " + arg);
                }
            }
        }

        public void deleteLogs(string days)
        {
            rproDBHandler.deleteZeroCost(days);
        }

        public void initialize()
        {
            rproDBHandler.createLogTableIfNotExists();
            rproDBHandler.createZeroCostTableIfNotExists();
        }

        public void runSlips()
        {
            SlipController controller = new SlipController(settings, rproDBHandler, msSqlDBHandler);
            controller.runUpdateSlips();
        }

        public void runInvoices()
        {
            InvoiceController controller = new InvoiceController(settings, rproDBHandler, msSqlDBHandler);
            controller.runUpdateInvoices();
        }

        public void runAdjustments()
        {
            AdjustmentController controller = new AdjustmentController(settings, rproDBHandler, msSqlDBHandler);
            controller.runUpdateAdjustments();
            
        }

        public void runVoucherDiscrepancies()
        {
            VoucherDiscrepancyController controller = new VoucherDiscrepancyController(settings, rproDBHandler, msSqlDBHandler);
            controller.runUpdateVoucherDiscrepancies();

        }

        public void runVoucherReturns()
        {
            VoucherReturnController controller = new VoucherReturnController(settings, rproDBHandler, msSqlDBHandler);
            controller.runUpdateVoucherReturns();

        }

        public void runSendDailyEmail()
        {
            EmailController controller = new EmailController(settings);
            controller.sendDailyEmail();
        }

        public void runItemCost()
        {
            ItemCostController controller = new ItemCostController(settings, rproDBHandler, msSqlDBHandler);
            controller.runItemCost();
        }

        public Queue<CostDifference> getCostDifferences()
        {
            ItemCostController controller = new ItemCostController(settings, rproDBHandler, msSqlDBHandler);
            return controller.getCostDifferences();
        }
        public void runStoreFullSync()
        {
            storeSyncError = 0;
            try
            {
                List<string[]> docs = rproDBHandler.getInTransitDocuments();
                if (storeSyncError > 0)
                {
                    return;
                }
                if (docs.Count > 0)
                {
                    StoreSyncConfirmationForm sscf = new StoreSyncConfirmationForm(docs);
                    sscf.ShowDialog();
                    if (!sscf.cont)
                    {
                        return;
                    }
                }

                Queue<StoreCost> storeCosts = rproDBHandler.getStoreCosts();
                msSqlDBHandler.insertStoreCost(storeCosts, rproDBHandler);
                string body = "Store sync completed with " + storeSyncError + " error(s).";

                rproDBHandler.addLog(MainController.LogType.REPORT, null, null, MainController.Features.STORE_SYNC, body, null);
            }
            catch (Exception e)
            {
                rproDBHandler.addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.STORE_SYNC, "Exception occurred when running store sync ", e);
                return;
            }
        }

        public enum Features
        {
            ADJUSTMENT,
            SALE,
            SLIP,
            VOU_DISCREPANCY,
            VOU_RETURN,
            ITEM_COST,
            STORE_SYNC,
            SEND_EMAIL,
            NONE
        }

        public enum LogType
        {
            REPORT,
            ERROR,
            ZEROCOST,
            EXCEPTION
        }

        public static Features getFeatureByName(string featurename)
        {
            return (Features)System.Enum.Parse(typeof(Features), featurename);
        }

    }

    public class ZeroCostDocument
    {
        public string doc_sid;
        public string alu;
        public MainController.Features feature;
    }

    public class StoreCost
    {
        public string subsidiary;
        public string storecode;
        public string value;
    }

}
