using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace B1SimplificationInterface
{
    public class RproDBHandler
    {
        public const string LOG_TABLE_NAME = "B1_LOG";
        public const string ZEROCOST_TABLE_NAME = "B1_ZEROCOST";
        Settings settings;
        public RproDBHandler(Settings settings)
        {
            this.settings = settings;
        }
        public string getConnectionString()
        {
            return String.Format("Data Source={0};User Id={1};Password={2};", getListenerString(), "reportuser", "report");
        }
        public string getListenerString()
        {
            return string.Format("(DESCRIPTION = (ADDRESS_LIST=(ADDRESS =(PROTOCOL = TCP)(HOST = {0})(PORT = {1})))(CONNECT_DATA = (SERVICE_NAME = {2})))", settings.getRproHostAddress(), 1521, "RPROODS");
        }
        public void getInvoiceDivisions(HashSet<string> invoiceSIDs, int days_before, string subsidiaryFIlter, Queue<ZeroCostDocument> zeroCostInvoiceItems, InvoiceController invc_controller)
        {
            if (!string.IsNullOrWhiteSpace(subsidiaryFIlter))
            {
                subsidiaryFIlter = " and inv.sbs_no in (" + subsidiaryFIlter + ") ";
            }
            string sql = "select inv.invc_sid, invn.alu, to_char(inv.created_date, 'yyyyMMdd') as inv_date, to_char(inv.store_no, '000') as store_code, inv.sbs_no, ";
            sql += "inv.invc_no, substr(invn.dcs_code, 0, 3) as division, sto.glob_store_code as cardcode, ";
            sql += "round(nvl(case when inv.invc_type = 2 then item.qty * invn.cost * -1 else item.qty * invn.cost end, 0), 2) as cost, nvl(invn.cost, 0) as unit_cost, ";
            sql += "round(case when inv.invc_type = 2 then(item.qty * (item.price - item.tax_amt)) * (100 - nvl(inv.disc_perc, 0)) / 100 * -1 ";
            sql += "else (item.qty * (item.price - tax_amt)) * (100 - nvl(inv.disc_perc, 0)) / 100 end, 2) as nett_sales ";
            sql += "from invoice_v inv inner join invc_item_v item on inv.invc_sid = item.invc_sid  ";
            sql += "inner join store_v sto on inv.store_no = sto.store_no and inv.sbs_no = sto.sbs_no ";
            sql += "inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 where inv.hisec_type is null and inv.status2 = 0 and sto.glob_store_code is not null ";
            sql += "and inv.created_date >trunc(sysdate)- " + days_before + subsidiaryFIlter + " order by invc_sid ";
            
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    Invoice currentInvoice = null;
                    while (reader.Read())
                    {
                        
                        InvoiceDivision inv_div = new InvoiceDivision(reader);
                        //skip if invoice has already been posted to B1
                        if (invoiceSIDs.Contains(inv_div.invc_sid))
                        {
                            continue;
                        }
                        // only for first iteration
                        if (currentInvoice == null)
                        {
                            currentInvoice = new Invoice(inv_div, zeroCostInvoiceItems);
                            continue;
                        }
                        // false if current record has a different invoice sid from the current holding invoice
                        if (!currentInvoice.addDivisionToInvoice(inv_div, zeroCostInvoiceItems))
                        {
                            //only add if the invoice is qualified (no zero cost)
                            if (!currentInvoice.hasZeroCost)
                            {
                                invc_controller.addInvoiceToGroup(currentInvoice);
                            }
                            currentInvoice = new Invoice(inv_div, zeroCostInvoiceItems);
                        }
                    }
                    if (currentInvoice != null && !currentInvoice.hasZeroCost)
                    {
                        invc_controller.addInvoiceToGroup(currentInvoice);
                    }
                }
                catch (Exception e)
                {
                    InvoiceController.error += 1;
                    addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.SALE, "An exception occurred when retrieving sales in Retail Pro.", e);
                }
                finally
                {
                    cn.Close();
                }
            }
        }


        public Queue<Slip> getSlips(HashSet<string> slipSIDs, int days_before, string subsidiaryFilter)
        {
            string sql = "select slip.slip_sid, slip.slip_no, com.comments, to_char(slip.modified_date, 'YYYYMMDD') as slip_date, to_char(out_store_no, '000') as from_store, ";
            sql += "slip.sbs_no as from_sbs, to_char(in_store_no, '000') as to_store, in_sbs_no as to_sbs, nvl(sum(item.qty * invn.cost), 0) as slip_value, ";
            sql += "max(case when nvl(invn.cost, 0) = 0 then 1 else 0 end) as zeroCost ";
            sql += "from slip_v slip inner join slip_item_v item on slip.slip_sid = item.slip_sid ";
            sql += "inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 ";
            sql += "left outer join vou_comment_v com on slip.vou_sid = com.vou_sid and com.comment_no = 1 ";
            sql += "where slip.verified = 1 and status2 = 0 and slip.modified_date >= trunc(sysdate) - " + days_before + subsidiaryFilter + " ";
            sql += "group by slip.slip_sid, slip.slip_no, com.comments, to_char(slip.modified_date, 'YYYYMMDD'), to_char(out_store_no, '000'), slip.sbs_no, to_char(in_store_no, '000'), in_sbs_no having max(case when nvl(invn.cost, 0) = 0 then 1 else 0 end) = 0";
            Queue<Slip> slips = new Queue<Slip>();
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string sid = reader["slip_sid"].ToString();
                        if (slipSIDs.Contains(sid))
                        {
                            continue;
                        }
                        Slip slip = new Slip();
                        slip.slip_sid = sid;
                        slip.slip_date = reader["slip_date"].ToString().Trim();
                        slip.slip_no = reader["slip_no"].ToString().Trim();
                        slip.from_store = reader["from_store"].ToString().Trim();
                        slip.from_sbs = reader["from_sbs"].ToString().Trim();
                        slip.to_store = reader["to_store"].ToString().Trim();
                        slip.to_sbs = reader["to_sbs"].ToString().Trim();
                        slip.slip_value = reader["slip_value"].ToString().Trim();
                        slip.comments = getStringVal(reader, "comments");
                        slips.Enqueue(slip);
                    }
                }
                catch (Exception e)
                {
                    SlipController.error += 1;
                    addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.SLIP, "An exception occurred when retrieving slips in Retail Pro.", e);
                }
                finally
                {
                    cn.Close();
                }
            }
            return slips;
        }

        public Dictionary<String, List<VoucherDiscrepancyItem>> getVoucherDiscrepancies(HashSet<string> sids, int days_before, string subsidiaryFilter)
        {
            /*
             * select vou.vou_sid, vou_no, to_char(vou.created_date, 'YYYYMMDD') as vou_date, to_char(vou.store_no, '000') as storeID, vou.sbs_no as subsidiary, 
                sum(item.qty) - sum(item.orig_qty) as qty, invn.alu, round(nvl(invn.cost, 0), 2), substr(invn.dcs_code, 0, 3) as division, sto.glob_store_code as cardcode, comm.COMMENTS as comments
                from voucher_v vou inner join vou_item_v item on item.vou_sid = vou.vou_sid
                inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1
                inner join store_v sto on vou.store_no = sto.store_no and vou.sbs_no = sto.sbs_no
                left outer join vou_comment_v comm on comm.vou_sid = vou.vou_sid and comm.COMMENT_NO = 1
                left outer join doc_reason_code_v reason on reason.sbs_no = vou.sbs_no and reason.doc_reason_id = vou.doc_reason_id 
                where vou_type = 0 and vou_class = 0 and status2 = 0 and slip_flag = 0 and asn_no is not null and (reason.doc_reason_code is null or reason.doc_reason_code != 'interface') and vou.created_date >trunc(sysdate)- 15
                group by vou.vou_sid, vou_no, to_char(vou.created_date, 'YYYYMMDD'), to_char(vou.store_no, '000'), vou.sbs_no, invn.alu, round(nvl(invn.cost, 0), 2), substr(invn.dcs_code, 0, 3), sto.glob_store_code, comm.comments
                having sum(item.qty) - sum(item.orig_qty) != 0;
            */
            string sql = "select vou.vou_sid, vou_no, to_char(vou.created_date, 'YYYYMMDD') as vou_date, to_char(vou.store_no, '000') as storeID, vou.sbs_no as subsidiary, ";
            sql += "sum(item.qty) - sum(item.orig_qty) as qty, invn.alu as alu, round(nvl(invn.cost, 0), 2) as cost, substr(invn.dcs_code, 0, 3) as division, sto.glob_store_code as cardcode, comm.COMMENTS as comments ";
            sql += "from voucher_v vou inner join vou_item_v item on item.vou_sid = vou.vou_sid ";
            sql += "inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 ";
            sql += "inner join store_v sto on vou.store_no = sto.store_no and vou.sbs_no = sto.sbs_no ";
            sql += "left outer join vou_comment_v comm on comm.vou_sid = vou.vou_sid and comm.COMMENT_NO = 1 ";
            sql += "left outer join doc_reason_code_v reason on reason.sbs_no = vou.sbs_no and reason.doc_reason_id = vou.doc_reason_id  ";
            sql += "where vou_type = 0 and vou_class = 0 and status2 = 0 and slip_flag = 0 and asn_no is not null and (reason.doc_reason_code is null or reason.doc_reason_code != 'interface') and vou.created_date >trunc(sysdate)- " + days_before + subsidiaryFilter + " ";
            sql += "group by vou.vou_sid, vou_no, to_char(vou.created_date, 'YYYYMMDD'), to_char(vou.store_no, '000'), vou.sbs_no, invn.alu, round(nvl(invn.cost, 0), 2), substr(invn.dcs_code, 0, 3), sto.glob_store_code, comm.comments ";
            sql += "having sum(item.qty) - sum(item.orig_qty) != 0 ";
            Dictionary<String, List<VoucherDiscrepancyItem>> vou_discrepancies = new Dictionary<String, List<VoucherDiscrepancyItem>>();
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string sid = reader["vou_sid"].ToString();
                        if (sids.Contains(sid))
                        {
                            continue;
                        }
                        VoucherDiscrepancyItem vou_disc = new VoucherDiscrepancyItem();
                        vou_disc.vou_sid = sid;
                        vou_disc.vou_date = reader["vou_date"].ToString().Trim();
                        vou_disc.vou_no = reader["vou_no"].ToString().Trim();
                        vou_disc.storeid = reader["storeID"].ToString().Trim();
                        vou_disc.subsidiary = reader["subsidiary"].ToString().Trim();
                        vou_disc.qty = reader["qty"].ToString().Trim();
                        vou_disc.alu = reader["alu"].ToString().Trim();
                        vou_disc.value = reader["cost"].ToString().Trim();
                        vou_disc.division = reader["division"].ToString().Trim();
                        vou_disc.cardcode = reader["cardcode"].ToString().Trim();
                        vou_disc.comments = getStringVal(reader, "comments");
                        vou_disc.calculateAbsoluteTotalCost();
                        if (vou_discrepancies.ContainsKey(sid))
                        {
                            List<VoucherDiscrepancyItem> vou_discrepancyItems = vou_discrepancies[sid];
                            vou_discrepancyItems.Add(vou_disc);
                        }
                        else
                        {
                            List<VoucherDiscrepancyItem> vou_discrepancyItems = new List<VoucherDiscrepancyItem>();
                            vou_discrepancyItems.Add(vou_disc);
                            vou_discrepancies[sid] = vou_discrepancyItems;
                        }
                    }
                }
                catch (Exception e)
                {
                    VoucherDiscrepancyController.error += 1;
                    addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.VOU_DISCREPANCY, "An exception occurred when retrieving voucher discrepancies in Retail Pro.", e);
                }
                finally
                {
                    cn.Close();
                }
            }
            return vou_discrepancies;
        }

        public Dictionary<String, List<VoucherReturnItem>> getVoucherReturnItems(HashSet<string> sids, int days_before, string subsidiaryFilter)
        {
            /*
            select vou.vou_sid, vou_no, to_char(vou.created_date, 'YYYYMMDD') as vou_date, to_char(vou.store_no, '000') as storeID, vou.sbs_no as subsidiary, reason.doc_reason_code as reason,
            sum(item.qty) as qty, invn.alu, round(nvl(invn.cost, 0), 2) as cost, substr(invn.dcs_code, 0, 3) as division, sto.glob_store_code as cardcode, comm.COMMENTS as comments, invn.vend_code
            from voucher_v vou inner join vou_item_v item on item.vou_sid = vou.vou_sid
            inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1
            inner join store_v sto on vou.store_no = sto.store_no and vou.sbs_no = sto.sbs_no
            left outer join doc_reason_code_v reason on vou.DOC_REASON_ID = reason.doc_reason_id and vou.sbs_no = reason.sbs_no 
            left outer join vou_comment_v comm on comm.vou_sid = vou.vou_sid and comm.COMMENT_NO = 1
            where vou_type = 1 and vou_class = 0 and status2 = 0 and slip_flag = 0 and (reason.doc_reason_code is null or reason.doc_reason_code != 'interface') and vou.created_date >trunc(sysdate)- 10
            group by vou.vou_sid, vou_no, to_char(vou.created_date, 'YYYYMMDD'), to_char(vou.store_no, '000'), vou.sbs_no, reason.doc_reason_code, 
            invn.alu, round(nvl(invn.cost, 0), 2), substr(invn.dcs_code, 0, 3), sto.glob_store_code, comm.comments, invn.vend_code;
            */
            string sql = "select vou.vou_sid, vou_no, to_char(vou.created_date, 'YYYYMMDD') as vou_date, to_char(vou.store_no, '000') as storeID, vou.sbs_no as subsidiary, reason.doc_reason_code as reason, ";
            sql += "sum(item.qty) as qty, invn.alu, round(nvl(invn.cost, 0), 2) as cost, substr(invn.dcs_code, 0, 3) as division, sto.glob_store_code as cardcode, comm.COMMENTS as comments, invn.vend_code ";
            sql += "from voucher_v vou inner join vou_item_v item on item.vou_sid = vou.vou_sid ";
            sql += "inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 ";
            sql += "inner join store_v sto on vou.store_no = sto.store_no and vou.sbs_no = sto.sbs_no ";
            sql += "left join doc_reason_code_v reason on vou.DOC_REASON_ID = reason.doc_reason_id and vou.sbs_no = reason.sbs_no ";
            sql += "left outer join vou_comment_v comm on comm.vou_sid = vou.vou_sid and comm.COMMENT_NO = 1 ";
            sql += "where vou_type = 1 and vou_class = 0 and status2 = 0 and slip_flag = 0 and (reason.doc_reason_code is null or reason.doc_reason_code != 'interface') and vou.created_date >trunc(sysdate)- " + days_before + subsidiaryFilter + " ";
            sql += "group by vou.vou_sid, vou_no, to_char(vou.created_date, 'YYYYMMDD'), to_char(vou.store_no, '000'), vou.sbs_no, reason.doc_reason_code,  ";
            sql += "invn.alu, round(nvl(invn.cost, 0), 2), substr(invn.dcs_code, 0, 3), sto.glob_store_code, comm.comments, invn.vend_code ";
            Dictionary<String, List<VoucherReturnItem>> vouchers = new Dictionary<String, List<VoucherReturnItem>>();
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string sid = reader["vou_sid"].ToString();
                        if (sids.Contains(sid))
                        {
                            continue;
                        }
                        VoucherReturnItem vou_item = new VoucherReturnItem();
                        vou_item.vou_sid = sid;
                        vou_item.vou_date = reader["vou_date"].ToString().Trim();
                        vou_item.vou_no = reader["vou_no"].ToString().Trim();
                        vou_item.storeid = reader["storeID"].ToString().Trim();
                        vou_item.subsidiary = reader["subsidiary"].ToString().Trim();
                        vou_item.qty = reader["qty"].ToString().Trim();
                        vou_item.alu = reader["alu"].ToString().Trim();
                        vou_item.value = reader["cost"].ToString().Trim();
                        vou_item.division = reader["division"].ToString().Trim();
                        vou_item.cardcode = reader["cardcode"].ToString().Trim();
                        vou_item.vend_code = reader["vend_code"].ToString().Trim();
                        vou_item.reason = reader["reason"].ToString().Trim();
                        vou_item.comments = getStringVal(reader, "comments");
                        if (string.IsNullOrWhiteSpace(vou_item.comments))
                        {
                            vou_item.comments = "";
                        }
                        if (vouchers.ContainsKey(sid))
                        {
                            List<VoucherReturnItem> vou_items = vouchers[sid];
                            vou_items.Add(vou_item);
                        }
                        else
                        {
                            List<VoucherReturnItem> vou_items = new List<VoucherReturnItem>();
                            vou_items.Add(vou_item);
                            vouchers[sid] = vou_items;
                        }
                    }
                }
                catch (Exception e)
                {
                    VoucherReturnController.error += 1;
                    addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.VOU_DISCREPANCY, "An exception occurred when retrieving voucher discrepancies in Retail Pro.", e);
                }
                finally
                {
                    cn.Close();
                }
            }
            return vouchers;
        }

        public Queue<StoreCost> getStoreCosts()
        {
            /*
            select round(sum(qty.qty * invn.cost), 2) as totalcost, qty.sbs_no as subsidiary, to_char(qty.store_no, '000') as storecode from invn_sbs_v invn 
            inner join invn_sbs_qty_v qty on invn.sbs_no = qty.sbs_no and invn.item_sid = qty.item_sid 
            where 1 = 1 
            group by qty.sbs_no, to_char(qty.store_no, '000');
             
             */
            string subsidiaryFilter = settings.getSubsidiaries(MainController.Features.STORE_SYNC);
            if (!string.IsNullOrWhiteSpace(subsidiaryFilter))
            {
                subsidiaryFilter = " and qty.sbs_no in (" + subsidiaryFilter + ") ";
            }
            string sql = "select round(sum(qty.qty * invn.cost), 2) as totalcost, qty.sbs_no as subsidiary, to_char(qty.store_no, '000') as storecode from invn_sbs_v invn ";
            sql += "inner join invn_sbs_qty_v qty on invn.sbs_no = qty.sbs_no and invn.item_sid = qty.item_sid ";
            sql += "where qty.store_no < 250 " + subsidiaryFilter;
            sql += "group by qty.sbs_no, to_char(qty.store_no, '000') "; 

            Queue<StoreCost> storecosts = new Queue<StoreCost>();
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        StoreCost storecost = new StoreCost();
                        storecost.storecode = reader["storecode"].ToString().Trim();
                        storecost.subsidiary = reader["subsidiary"].ToString().Trim();
                        storecost.value = reader["totalcost"].ToString().Trim();
                        storecosts.Enqueue(storecost);
                    }
                }
                catch (Exception e)
                {
         
                    addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.STORE_SYNC, "An exception occurred when retrieving store sync  costs in Retail Pro.", e);
                }
                finally
                {
                    cn.Close();
                }
            }
            return storecosts;
        }
        public Dictionary<string, List<Adj_div>> getAdjustments(HashSet<string> adjSIDs, int days_before, string subsidiaryFilter)
        {
            /*
             * with adj as (select distinct item.adj_sid, max(case when invn.cost = 0 then 1 else 0 end) as zeroCostFlag,adj.adj_no as adj_no,
                to_char(adj.created_date, 'YYYYMMDD') as adj_date, adj.sbs_no as sbs_no, adj.store_no as store_no, case when adj.adj_reason_name = 'Uniform' then 0 else adj.creating_doc_type end  as creating_doc_type
                from adj_item_v item inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1
                inner join adjustment_v adj on item.adj_sid = adj.adj_sid
                where adj.adj_type = 0 and adj.status = 0 and adj.held = 0 and adj.isreversed = 0 and adj.creating_doc_type!=9 and adj.created_date >=trunc(sysdate)- 10
                group by item.adj_sid, adj.adj_no, to_char(adj.created_date, 'YYYYMMDD'), adj.sbs_no , adj.store_no, case when adj.adj_reason_name = 'Uniform' then 0 else adj.creating_doc_type end
                having max(case when invn.cost = 0 then 1 else 0 end) = 0)
                select 
                adj.adj_sid as adj_sid,
                adj.adj_date as adj_date,
                adj.adj_no as adj_no,
                to_char(adj.store_no, '000') as storecode,
                adj.sbs_no as subsidiary,
                sto.glob_store_code as cardcode,
                sum((item.adj_value - item.orig_value) * invn.cost) as totalvalue,
                substr(invn.dcs_code, 0, 3) as division,
                adj.creating_doc_type as creating_doc_type,
                comm.comments as comments
                from adj adj inner join adj_item_v item on adj.adj_sid = item.adj_sid
                left outer join adj_comment_v comm on adj.adj_sid = comm.adj_sid and comm.comment_no = 1
                inner join store_v sto on adj.store_no = sto.store_no and adj.sbs_no = sto.sbs_no
                inner join inventory_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1
                group by 
                adj.adj_sid,
                adj.adj_date,
                adj.adj_no,
                to_char(adj.store_no, '000'),
                adj.sbs_no,
                sto.glob_store_code,
                substr(invn.dcs_code, 0, 3),
                adj.creating_doc_type,
                comm.comments;
                */
            Dictionary<string, List<Adj_div>> adjustments = new Dictionary<string, List<Adj_div>>();
            string sql = "with adj as (select distinct item.adj_sid, max(case when invn.cost = 0 then 1 else 0 end) as zeroCostFlag,adj.adj_no as adj_no, ";
            sql += "to_char(adj.created_date, 'YYYYMMDD') as adj_date, adj.sbs_no as sbs_no, adj.store_no as store_no, case when adj.adj_reason_name = 'Uniform' then 0 else adj.creating_doc_type end  as creating_doc_type ";
            sql += "from adj_item_v item inner join invn_sbs_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 ";
            sql += "inner join adjustment_v adj on item.adj_sid = adj.adj_sid ";
            sql += "where adj.adj_type = 0 and adj.status = 0 and adj.held = 0 and adj.isreversed = 0 and adj.creating_doc_type!=9 and adj.created_date >=trunc(sysdate)-  " + days_before + subsidiaryFilter + " ";
            sql += "group by item.adj_sid, adj.adj_no, to_char(adj.created_date, 'YYYYMMDD'), adj.sbs_no , adj.store_no,  case when adj.adj_reason_name = 'Uniform' then 0 else adj.creating_doc_type end ";
            sql += "having max(case when invn.cost = 0 then 1 else 0 end) = 0) ";
            sql += "select adj.adj_sid as adj_sid, adj.adj_date as adj_date, adj.adj_no as adj_no, to_char(adj.store_no, '000') as storecode, ";
            sql += "adj.sbs_no as subsidiary, sto.glob_store_code as cardcode, round(sum((item.adj_value - item.orig_value) * invn.cost), 2) as totalvalue, ";
            sql += "substr(invn.dcs_code, 0, 3) as division, adj.creating_doc_type as creating_doc_type, comm.comments as comments ";
            sql += "from adj adj inner join adj_item_v item on adj.adj_sid = item.adj_sid ";
            sql += "left outer join adj_comment_v comm on adj.adj_sid = comm.adj_sid and comm.comment_no = 1 ";
            sql += "inner join store_v sto on adj.store_no = sto.store_no and adj.sbs_no = sto.sbs_no ";
            sql += "inner join inventory_v invn on item.item_sid = invn.item_sid and invn.sbs_no = 1 ";
            sql += "group by adj.adj_sid,adj.adj_date,adj.adj_no,to_char(adj.store_no, '000'),adj.sbs_no,sto.glob_store_code,substr(invn.dcs_code, 0, 3),adj.creating_doc_type,comm.comments ";

            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string sid = reader["adj_sid"].ToString();
                        if (adjSIDs.Contains(sid))
                        {
                            continue;
                        }
                        Adj_div adj_div = new Adj_div();
                        adj_div.adj_sid = sid;
                        adj_div.adj_date = reader["adj_date"].ToString().Trim();
                        adj_div.adj_no = reader["adj_no"].ToString().Trim();
                        adj_div.cardcode = getStringVal(reader, "cardcode");
                        adj_div.division = reader["division"].ToString().Trim();
                        adj_div.creating_doc_type = reader["creating_doc_type"].ToString().Trim();
                        adj_div.storecode = reader["storecode"].ToString().Trim();
                        adj_div.subsidiary = reader["subsidiary"].ToString().Trim();
                        adj_div.totalvalue = reader["totalvalue"].ToString().Trim();
                        adj_div.comments = getStringVal(reader, "comments");
                        if (adjustments.ContainsKey(sid))
                        {
                            List<Adj_div> adjustment = adjustments[sid];
                            adjustment.Add(adj_div);
                        }
                        else
                        {
                            List<Adj_div> adjustment = new List<Adj_div>();
                            adjustment.Add(adj_div);
                            adjustments[sid] = adjustment;
                        }
                    }
                }
                catch (Exception e)
                {
                    AdjustmentController.error += 1;
                    addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.ADJUSTMENT, "An exception occurred when retrieving adjustments in Retail Pro.", e);
                }
                finally
                {
                    cn.Close();
                }
            }
            return adjustments;
        }

        public Queue<ZeroCostDocument> getZeroCostDocument(string sql, MainController.Features feature)
        {
            Queue<ZeroCostDocument> zeroCostDocuments = new Queue<ZeroCostDocument>();
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        ZeroCostDocument zeroCostDocument = new ZeroCostDocument();
                        zeroCostDocument.doc_sid = reader["doc_sid"].ToString();
                        zeroCostDocument.alu = reader["alu"].ToString();
                        zeroCostDocument.feature = MainController.Features.SLIP;
                        zeroCostDocuments.Enqueue(zeroCostDocument);
                    }
                }
                catch (Exception e)
                {
                    addZeroCostErrorCount(feature);
                    addLog(MainController.LogType.EXCEPTION, null, null, feature, "Error fetching " + feature.ToString().ToLower() + " with zero cost. " + e.Message, e);
                }
                finally
                {
                    cn.Close();
                }
            }
            return zeroCostDocuments;
        }

        public void insertZeroCostDocuments(Queue<ZeroCostDocument> zeroCostDocuments, MainController.Features feature)
        {
            if (zeroCostDocuments.Count == 0)
            {
                return;
            }
            string[] alus = new string[zeroCostDocuments.Count];
            string[] doc_sids = new string[zeroCostDocuments.Count];
            string[] features = new string[zeroCostDocuments.Count];
            string[] message = new string[zeroCostDocuments.Count];
            string[] logtype = new string[zeroCostDocuments.Count];
            int i = 0;
            while (zeroCostDocuments.Count > 0)
            {
                ZeroCostDocument doc = zeroCostDocuments.Dequeue();
                alus[i] = doc.alu;
                doc_sids[i] = doc.doc_sid;
                features[i] = feature.ToString();
                message[i] = "Document item has zero cost.";
                logtype[i] = MainController.LogType.ZEROCOST.ToString();
                i++;
            }
            String sql = "insert into " + ZEROCOST_TABLE_NAME + "(doc_sid, alu, feature_name) VALUES(:doc_sid, :alu, :feature_name)";
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cmd.ArrayBindCount = alus.Length;
                    //iserror, logtype, module1,module2,message1, message2, desc1, desc2
                    cmd.Parameters.Add(getOracleArrayParameter(doc_sids));
                    cmd.Parameters.Add(getOracleArrayParameter(alus));
                    cmd.Parameters.Add(getOracleArrayParameter(features));
                    cn.Open();
                    int result = cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    addZeroCostErrorCount(feature);
                    addLog(MainController.LogType.EXCEPTION, null, null, feature, "An exception occurred when update zero cost documents in Retail Pro.", e);
                }
                finally
                {
                    cn.Close();
                }
            }
        }
        public string getServerDate(RproDBHandler rproDBHandler)
        {
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    cn.Open();
                    string sql = "SELECT SYSTIMESTAMP AS SYSTIMESTAMP FROM DUAL";
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    OracleDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            DateTime dt = DateTime.Parse(reader["SYSTIMESTAMP"].ToString());
                            TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById("US Eastern Standard Time");
                            TimeSpan offset = tzi.GetUtcOffset(dt);
                            string serverDateTime = dt.ToString("yyyy-MM-ddTHH:mm:ss") + offset.ToString();
                            return serverDateTime.Substring(0, serverDateTime.Length - 3);
                        }
                    }
                }
                catch (Exception e)
                {
                    addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.ITEM_COST, "Unable to fetch server date from Retail Pro. Run aborted.", e);
                }
                finally
                {
                    cn.Close();
                }
                return null;
            }
        }
        private void addZeroCostErrorCount(MainController.Features feature)
        {
            switch (feature)
            {
                case MainController.Features.ADJUSTMENT:
                    AdjustmentController.zeroCostError += 1;
                    break;
                case MainController.Features.SALE:
                    InvoiceController.zeroCostError += 1;
                    break;
                case MainController.Features.SLIP:
                    SlipController.zeroCostError += 1;
                    break;
                case MainController.Features.VOU_DISCREPANCY:
                    VoucherDiscrepancyController.zeroCostError += 1;
                    break;
            }
        }

        public Queue<CostDifference> matchItemCost(Dictionary<string, double> b1_cost)
        {
            string sql = "select item_sid, alu, nvl(cost, 0) as cost from invn_sbs_v where sbs_no = 1";
            Queue<CostDifference> updates = new Queue<CostDifference>();
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string item_sid = reader["item_sid"].ToString().Trim();
                        string alu = reader["alu"].ToString().Trim();
                        double cost = Convert.ToDouble(reader["cost"]);
                        if (b1_cost.ContainsKey(item_sid))
                        {
                            if (b1_cost[item_sid] != cost)
                            {
                                updates.Enqueue(new CostDifference(item_sid, alu, b1_cost[item_sid], cost));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.ITEM_COST, "An exception occurred when fetching item cost in Retail Pro.", e);
                }
                finally
                {
                    cn.Close();
                }
            }
            return updates;
        }

        public List<string[]> getInTransitDocuments()
        {
            List<string[]> list = new List<string[]>();
            string subsidiaries = settings.getSubsidiaries(MainController.Features.STORE_SYNC);
            string sql = "";
            if (!string.IsNullOrWhiteSpace(subsidiaries))
            {
                sql = "select 'ASN' as type, sbs_no, store_no, vou_sid as doc_sid, vou_no as doc_no, to_char(created_date, 'yyyy-mm-dd') as doc_date from voucher_v where vou_class = 2 and status2 = 0 and sbs_no in (" + subsidiaries + ") ";
                sql += "union ";
                sql += "select 'SLIP' as type, in_sbs_no as sbs_no, in_store_no as store_no, slip_sid as doc_sid, slip_no as doc_no, to_char(created_date, 'yyyy-mm-dd') as doc_date from slip_v where unverified = 1 and status2 = 0 and sbs_no in (" + subsidiaries + ") ";
            }
           else
            {
                sql = "select 'ASN' as type, sbs_no, store_no, vou_sid as doc_sid, vou_no as doc_no, to_char(created_date, 'yyyy-mm-dd') as doc_date from voucher_v where vou_class = 2 and status2 = 0 ";
                sql += "union ";
                sql += "select 'SLIP' as type, in_sbs_no as sbs_no, in_store_no as store_no, slip_sid as doc_sid, slip_no as doc_no, to_char(created_date, 'yyyy-mm-dd') as doc_date from slip_v where unverified = 1 and status2 = 0 ";
            }
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            string[] data = new string[6];
                            data[0] = getStringVal(reader, "type");
                            data[1] = getStringVal(reader, "sbs_no");
                            data[2] = getStringVal(reader, "store_no");
                            data[3] = getStringVal(reader, "doc_no");
                            data[4] = getStringVal(reader, "doc_date");
                            data[5] = getStringVal(reader, "doc_sid");
                            list.Add(data);
                        }
                    }
                }
                catch (Exception e)
                {
                    addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.STORE_SYNC, "An exception occurred when fetching ASNs and unverified SLIPs in Retail Pro.", e);
                    MainController.storeSyncError += 1;
                    throw e;
                }
                finally
                {
                    cn.Close();
                }
            }
            return list;
        }

        public List<string[]> getLogDetails(string filter)
        {
            List<string[]> list = new List<string[]>();
            String sql = "select to_char(date1, 'yyyy-mm-dd') as log_date, to_char(date1, 'HH24:MI:SS') as time1, logtype, feature_name, message, doc_sid, alu, stacktrace from "+LOG_TABLE_NAME+ " " + filter + " order by date1 desc";
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            string[] data = new string[8];
                            data[0] = getStringVal(reader, "log_date");
                            data[1] = getStringVal(reader, "time1");
                            data[2] = getStringVal(reader, "logtype");
                            data[3] = getStringVal(reader, "feature_name");
                            data[4] = getStringVal(reader, "message");
                            data[5] = getStringVal(reader, "doc_sid");
                            data[6] = getStringVal(reader, "alu");
                            data[7] = getStringVal(reader, "stacktrace");
                            list.Add(data);
                        }
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("Unable to fetch log details. " + e.Message + "\nStacktrace: " + e.StackTrace);
                }
                finally
                {
                    cn.Close();
                }
            }
            return list;
        }

        public void deleteZeroCost(string daysAgo)
        {
            String sql = "delete from " + ZEROCOST_TABLE_NAME + " where date1 <trunc(sysdate)- " + daysAgo;
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error occurred when deleting zero cost. Message: " + e.Message, "stacktrace: " + e.StackTrace);
                }
                finally
                {
                    cn.Close();
                }
            }
        }

        public List<string[]> getZeroCost(string filter)
        {
            List<string[]> list = new List<string[]>();
            String sql = "select doc_sid, alu, feature_name, to_char(max(date1), 'yyyy-mm-dd HH24:MI:SS') as latest_date from " + ZEROCOST_TABLE_NAME + " " + filter + " group by doc_sid, alu, feature_name order by max(date1)";
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            string[] data = new string[8];
                            data[0] = getStringVal(reader, "feature_name");
                            data[1] = getStringVal(reader, "alu");
                            data[2] = getStringVal(reader, "doc_sid");
                            data[3] = getStringVal(reader, "latest_date");
                            list.Add(data);
                        }
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("Unable to fetch zerocost details. " + e.Message + "\nStacktrace: " + e.StackTrace);
                }
                finally
                {
                    cn.Close();
                }
            }
            return list;
        }

        private OracleParameter getOracleArrayParameter(string[] input)
        {
            OracleParameter param = new OracleParameter();
            param.OracleDbType = OracleDbType.NVarchar2;
            param.Value = input;
            return param;
        }

        private static string getStringVal(OracleDataReader reader, string key)
        {
            if (reader[key] == System.DBNull.Value)
            {
                return null;
            }
            return reader[key].ToString().Trim();
        }


        public void addLog(MainController.LogType logtype, string doc_sid, string alu, MainController.Features feature_name, string message, Exception ex)
        {
            String sql = "insert into " + LOG_TABLE_NAME + "(logtype, doc_sid, alu, feature_name, message, stacktrace) VALUES(:logtype, :doc_sid, :alu, :feature_name, :message, :stacktrace)";
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    if (message.Length > 500)
                    {
                        message = message.Substring(0, 499);
                    }
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    //iserror, logtype, module1,module2,message1, message2, desc1, desc2
                    cmd.Parameters.Add(new OracleParameter("logtype", logtype.ToString()));
                    cmd.Parameters.Add(new OracleParameter("doc_sid", doc_sid));
                    cmd.Parameters.Add(new OracleParameter("alu", alu));
                    cmd.Parameters.Add(new OracleParameter("feature_name", feature_name.ToString()));
                    cmd.Parameters.Add(new OracleParameter("message", message));
                    string stacktrace = "";
                    if (ex != null)
                    {
                        stacktrace = ex.ToString();
                        if (stacktrace.Length > 1000)
                        {
                            stacktrace = stacktrace.Substring(0, 999);
                        }

                    }
                    cmd.Parameters.Add(new OracleParameter("stacktrace", stacktrace));
                    cn.Open();
                    int result = cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {

                }
                finally
                {
                    cn.Close();
                }
            }
        }

        public bool createLogTableIfNotExists()
        {

            string sql = "select table_name from user_tables where table_name in('" + LOG_TABLE_NAME + "')";
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        return true; //table already exists
                    }

                    sql = "";
                    sql += "CREATE TABLE " + LOG_TABLE_NAME;
                    sql += "(";
                    sql += "DOC_SID NVARCHAR2(30),";
                    sql += "ALU NVARCHAR2(30),";
                    sql += "LOGTYPE NVARCHAR2(10),"; //SUCCESS, ERROR, EXCEPTION
                    sql += "FEATURE_NAME NVARCHAR2(50),";
                    sql += "MESSAGE NVARCHAR2(500),";
                    sql += "STACKTRACE NVARCHAR2(1000),";
                    sql += "DATE1 TIMESTAMP(6) DEFAULT (sysdate) NOT NULL";
                    sql += ")NOLOGGING";
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();

                }
                catch (Exception e)
                {
                    MessageBox.Show("Oracle error:" + e.ToString());
                    Console.WriteLine("Unable to connect to database.");
                    return false;
                }
                finally
                {
                    cn.Close();

                }
                return true;
            }
        }

        public bool createZeroCostTableIfNotExists()
        {

            string sql = "select table_name from user_tables where table_name in('" + ZEROCOST_TABLE_NAME + "')";
            using (OracleConnection cn = new OracleConnection(getConnectionString()))
            {
                try
                {
                    OracleCommand cmd = new OracleCommand(sql, cn);
                    cn.Open();
                    OracleDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        return true; //table already exists
                    }

                    sql = "";
                    sql += "CREATE TABLE " + ZEROCOST_TABLE_NAME;
                    sql += "(";
                    sql += "DOC_SID NVARCHAR2(30),";
                    sql += "ALU NVARCHAR2(30),";
                    sql += "FEATURE_NAME NVARCHAR2(50),";
                    sql += "DATE1 TIMESTAMP(6) DEFAULT (sysdate) NOT NULL";
                    sql += ")NOLOGGING";
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();

                }
                catch (Exception e)
                {
                    MessageBox.Show("Oracle error:" + e.ToString());
                    Console.WriteLine("Unable to connect to database.");
                    return false;
                }
                finally
                {
                    cn.Close();

                }
                return true;
            }
        }

    }
}
