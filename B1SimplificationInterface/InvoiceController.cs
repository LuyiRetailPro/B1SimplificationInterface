using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B1SimplificationInterface
{
    /* SQLS:
    with tb as (select item.invc_sid, max (case when nvl(invn.cost, 0) = 0 then 1 else 0 end) as zero_cost from invc_item_v item 
    inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 group by item.invc_sid)
    select inv.invc_sid, invn.alu, to_char(inv.created_date, 'yyyyMMdd') as inv_date, to_char(inv.store_no, '000') as store_code, inv.sbs_no, 
    inv.invc_no, substr(invn.dcs_code, 0,3) as division, tb.zero_cost,
    nvl(case when inv.invc_type = 2 then item.qty * invn.cost * -1 else item.qty * invn.cost end, 0) as cost, nvl(invn.cost, 0) as unit_cost,
    case when inv.invc_type = 2 then (item.qty * (item.price - item.tax_amt)) * (100 - nvl(inv.disc_perc, 0))/100 * -1
    else (item.qty * (item.price - tax_amt)) * (100 - nvl(inv.disc_perc, 0))/100 end as nett_sales
    from invoice_v inv inner join invc_item_v item on inv.invc_sid = item.invc_sid
    inner join tb on item.invc_sid = tb.invc_sid 
    inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 where inv.hisec_type is null and inv.status2 = 0
    and inv.created_date >trunc(sysdate)-30 order by invc_sid;
    */
    public class InvoiceController
    {
        Settings settings;
        public static int error;
        public static int zeroCostError;
        public Dictionary<string, List<Invoice>> invoiceGroups = new Dictionary<string, List<Invoice>>();
        RproDBHandler rproDBHandler;
        MsSqlDBHandler msSqlDBHandler;
        MainController.Features feature = MainController.Features.SALE;
        public InvoiceController(Settings settings, RproDBHandler rproDBHandler, MsSqlDBHandler mySqlDBHandler)
        {
            this.settings = settings;
            this.rproDBHandler = rproDBHandler;
            this.msSqlDBHandler = mySqlDBHandler;
        }

        public void runUpdateInvoices()
        {
            error = 0;
            zeroCostError = 0;
            int invoice_days = Int32.Parse(settings.getDays(feature));
            string day_limit = DateTime.Now.AddDays((invoice_days+1) * -1).Date.ToString(MsSqlDBHandler.DATE_FORMAT);
            Queue<ZeroCostDocument> zeroCostInvoices = new Queue<ZeroCostDocument>();
            string sql = "select distinct invc_sid as sid from Retailpro_Sales_Sync where date >= " + day_limit;
            HashSet<string> invoiceSIDs = null;
            try
            {
                invoiceSIDs = msSqlDBHandler.getExistingSIDs(sql, rproDBHandler, feature);
            }
            catch (Exception)
            {
                return;
            }
            string subsidiaryFilter = settings.getSubsidiaries(feature);
            rproDBHandler.getInvoiceDivisions(invoiceSIDs, invoice_days, subsidiaryFilter, zeroCostInvoices, this);
            //add invoice divisions to mysql table
            // select into invoice table using transactions
            int invoiceCount = 0;
            foreach (List<Invoice> inv_group in invoiceGroups.Values)
            {
                if (inv_group.Count < 1)
                {
                    continue;
                }
                string inv_group_name = "INV" + inv_group[0].invc_sid;
                foreach (Invoice inv in inv_group)
                {
                    invoiceCount += 1;
                    msSqlDBHandler.insertSalesSync(inv, inv_group_name, rproDBHandler);
                }
            }
            //insert into sales table from sales_sync table
            msSqlDBHandler.insertSales(rproDBHandler);
            int zeroCostTotal = zeroCostInvoices.Count;
            rproDBHandler.insertZeroCostDocuments(zeroCostInvoices, feature);

            string msg = invoiceCount + " Invoices fetched and inserted into B1 with " + error + " error(s). ";
            msg += zeroCostTotal + " items with zero cost were inserted with " + zeroCostError + " errors.";
            rproDBHandler.addLog(MainController.LogType.REPORT, "", "", feature, msg, null);

            if (error > 0 || zeroCostTotal > 0 )
            {
                string subject = "Errors/Zero cost in B1 Interface for " + MainController.Features.SALE.ToString();
                string body = "There are " + error + " errors when processing " + MainController.Features.SALE.ToString() + " on " + DateTime.Now.ToString() + ". \n";
                body += zeroCostTotal + " items with zero cost were inserted with " + zeroCostError + " errors.\n";
                body += "Please check log for details.";
                new EmailController(settings).sendEmail(subject, body, rproDBHandler, feature);
            }
        }

        public void addInvoiceToGroup(Invoice inv)
        {
            string group_name = inv.group;
            if (invoiceGroups.ContainsKey(group_name))
            {
                List<Invoice> list = invoiceGroups[group_name];
                list.Add(inv);
            }
            else
            {
                List<Invoice> list = new List<Invoice>();
                list.Add(inv);
                invoiceGroups.Add(group_name, list);
            }
        }
    }

    public class Invoice
    {
        public List<InvoiceDivision> divisions = new List<InvoiceDivision>();
        public string invc_sid;
        public string group;
        public bool hasZeroCost = false;
        public Invoice(InvoiceDivision inv_div, Queue<ZeroCostDocument> zeroCostInvoiceItems)
        {
            invc_sid = inv_div.invc_sid;
            group = inv_div.getGroup();
            addDivisionToInvoice(inv_div, zeroCostInvoiceItems);
        }
        public bool addDivisionToInvoice(InvoiceDivision inv_div, Queue<ZeroCostDocument> zeroCostInvoiceItems)
        {
            if (invc_sid != inv_div.invc_sid)
            {
                return false;
            }
            if (inv_div.isZeroCost())
            {
                if (!hasZeroCost)
                {
                    hasZeroCost = true;
                    divisions.Clear();
                }
                ZeroCostDocument doc = new ZeroCostDocument();
                doc.alu = inv_div.alu;
                doc.doc_sid = inv_div.invc_sid;
                doc.feature = MainController.Features.SALE;
                zeroCostInvoiceItems.Enqueue(doc);
            }
            else
            {
                divisions.Add(inv_div);
            }
            return true;
        }
    }

    public  class InvoiceDivision
    {
        public string invc_sid;
        public string invc_date;
        public string storeCode;
        public string sbs_no;
        public string cost;
        public string sales;
        public string division;
        public string unit_cost;
        public string cardcode;
        public string alu;
        public string getGroup()
        {
            return invc_date + "-" + String.Format(sbs_no,"000") + storeCode;
        }
        public InvoiceDivision(OracleDataReader reader)
        {
            invc_sid = reader["invc_sid"].ToString().Trim();
            invc_date = reader["inv_date"].ToString().Trim();
            storeCode = reader["store_code"].ToString().Trim();
            sbs_no = reader["sbs_no"].ToString().Trim();
            alu = reader["alu"].ToString().Trim();
            division = reader["division"].ToString().Trim();
            cost = reader["cost"].ToString().Trim();
            sales = reader["nett_sales"].ToString().Trim().Replace("-", "");
            unit_cost = reader["unit_cost"].ToString().Trim();
            cardcode = reader["cardcode"].ToString().Trim();
        }
        public bool isZeroCost()
        {
            return unit_cost == "0";
        }

        public string getAbsoluteCost()
        {
            return cost.Replace("-", "");
        }

        public string getSign()
        {
            if (cost.Contains("-"))
            {
                return "Minus";
            }
            return "Plus";
        }

        public string getSignSingle()
        {
            if (cost.Contains("-"))
            {
                return "M";
            }
            return "P";
        }
    }
}
