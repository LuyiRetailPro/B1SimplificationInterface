using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Data.SqlClient;

namespace B1SimplificationInterface
{
    public class MsSqlDBHandler
    {
        //Server=localhost;Database=master;Trusted_Connection=True;
        public const string DATE_FORMAT = "yyyyMMdd";
        //public const string connstring = "Server=localhost; database=RetailPro; UID=root; password=sysadmin";
        public Settings settings;

        public MsSqlDBHandler(Settings settings)
        {
            this.settings = settings;
        }

        private string getConnectionString()
        {
            return string.Format("Server={0}; database=RetailPro; UID={1}; password={2}", settings.getB1HostAddress(), settings.getB1Username(), settings.getB1Password());
        }

        public void insertSlips(Queue<Slip> slips, RproDBHandler rproDBHandler)
        {
            using (SqlConnection connection = new SqlConnection(getConnectionString()))
            {
                while (slips.Count > 0)
                {
                    Slip slip = slips.Dequeue();
                    try
                    {
                        /*
                        bool shouldUpdate = validateStoresAndSubsidiaries(slip.from_store, slip.from_sbs) && validateStoresAndSubsidiaries(slip.to_store, slip.to_sbs);
                        if (!shouldUpdate)
                        {
                            continue;
                        }
                        */
                        connection.Open();
                        string sql = "Insert into RetailPro_Slip (slip_sid, slip_date, fromstore, fromsbs, tostore, tosbs,slip_value) values (@slip_sid, @slip_date, @from_store, @from_sbs, @to_store, @to_sbs, @slip_value)";
                        SqlCommand cmd = new SqlCommand(sql, connection);
                        cmd.Parameters.Add(new SqlParameter("@slip_sid","SLP"+slip.slip_sid));
                        cmd.Parameters.Add(new SqlParameter("@slip_date", slip.slip_date));
                        cmd.Parameters.Add(new SqlParameter("@from_store", slip.from_store));
                        cmd.Parameters.Add(new SqlParameter("@from_sbs", slip.from_sbs));
                        cmd.Parameters.Add(new SqlParameter("@to_store", slip.to_store));
                        cmd.Parameters.Add(new SqlParameter("@to_sbs", slip.to_sbs));
                        Double slip_value = Double.Parse(slip.slip_value);
                        cmd.Parameters.Add(new SqlParameter("@slip_value", slip_value.ToString("0.00")));
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        SlipController.error += 1;
                        rproDBHandler.addLog(MainController.LogType.EXCEPTION, slip.slip_sid, null, MainController.Features.SLIP, "Exception caught when inserting slip " + slip.slip_sid, e);
                    }
                    finally
                    {
                        connection.Close();
                    }
                }
            }
        }

        public void insertSalesSync(Invoice invoice, string invc_sid, RproDBHandler rproDBHandler)
        {
            if (invoice.divisions.Count == 0 || invoice.hasZeroCost)
            {

                return;
            }
            using(SqlConnection connection = new SqlConnection(getConnectionString()))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();
                SqlTransaction transaction = connection.BeginTransaction("InvoiceTransaction");
                command.Connection = connection;
                command.Transaction = transaction;

                try
                {
                    string sql = "insert into RetailPro_Sales_Sync(SID, invc_sid, date, storecode, subsidiary, cardcode, sign, totalcost, totalnetsales, division) ";
                    sql += "values (@SID, @invc_sid, @date, @storecode, @subsidiary, @cardcode, @sign, @totalcost, @totalnetsales, @division) ";
                    command.CommandText = sql;
                    foreach (InvoiceDivision div in invoice.divisions)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@SID", invc_sid+div.getSignSingle());
                        command.Parameters.AddWithValue("@invc_sid", div.invc_sid);
                        command.Parameters.AddWithValue("@date", div.invc_date);
                        command.Parameters.AddWithValue("@storecode", div.storeCode);
                        command.Parameters.AddWithValue("@subsidiary", div.sbs_no);
                        command.Parameters.AddWithValue("@cardcode", div.cardcode);
                        command.Parameters.AddWithValue("@sign", div.getSign());
                        command.Parameters.AddWithValue("@totalcost", div.getAbsoluteCost());
                        command.Parameters.AddWithValue("@totalnetsales", div.sales);
                        command.Parameters.AddWithValue("@division", div.division);
                        command.ExecuteNonQuery();
                    }
                    // Attempt to commit the transaction.
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    rproDBHandler.addLog(MainController.LogType.EXCEPTION, invc_sid, null, MainController.Features.SALE, "Exception occurred when inserting invoice " + invoice.invc_sid, ex);
                    InvoiceController.error += 1;
                    // Attempt to roll back the transaction. 
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception ex2)
                    {
                        rproDBHandler.addLog(MainController.LogType.EXCEPTION, invc_sid, null, MainController.Features.SALE, "Exception occurred when performing rollback on invoice " + invoice.invc_sid, ex2);
                    }
                }
            }
        }

        public void insertSales(RproDBHandler rproDBHandler)
        {

            using (SqlConnection connection = new SqlConnection(getConnectionString()))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();
                SqlTransaction transaction = connection.BeginTransaction("SalesTransaction");
                command.Connection = connection;
                command.Transaction = transaction;

                try
                {
                    string sql = "insert into RetailPro_Sales(sid, date, storecode, subsidiary, cardcode, sign, totalcost, totalnetsales, division) ";
                    sql += "select sid, date, storecode, subsidiary, cardcode, sign, sum(totalcost) as totalcost, sum(totalnetsales) as totalnetsales, division from Retailpro_Sales_Sync ";
                    sql += "where processed='N' group by sid, date, storecode, subsidiary, cardcode, sign, division ";
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                    command.CommandText = "update RetailPro_Sales_Sync set processed = 'Y' where processed = 'N'";
                    command.ExecuteNonQuery();
                    // Attempt to commit the transaction.
                    transaction.Commit();
                    
                }
                catch (Exception ex)
                {
                    rproDBHandler.addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.SALE, "Exception occurred when updating sales table from sales_sync table ", ex);
                    InvoiceController.error += 1;
                    // Attempt to roll back the transaction. 
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception ex2)
                    {
                        rproDBHandler.addLog(MainController.LogType.EXCEPTION, null, null, MainController.Features.SALE, "Exception occurred when performing rollback sales_sync to sales table ", ex2);
                    }
                }
            }
        }

        public Dictionary<string, double> getItemCosts(RproDBHandler rproDBHandler)
        {
            string sql = "select sid, nullif(cost, 0.0) as cost from BES_TNotification ";
            Dictionary<string, double> itemCost = new Dictionary<string, double>();
            string sid = ""; ;

            using (SqlConnection connection = new SqlConnection(getConnectionString()))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(sql, connection);
                try
                {
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        sid = reader["sid"].ToString();
                        double cost = 0;
                        if (reader["cost"] != DBNull.Value)
                        {
                            cost = Convert.ToDouble(reader["cost"]);
                        }

                        itemCost[sid] = cost;
                    }
                }

                catch (Exception ex)
                {
                    rproDBHandler.addLog(MainController.LogType.EXCEPTION, null, sid, MainController.Features.ITEM_COST, "Exception occurred when fetching item cost from B1", ex);
                    ItemCostController.error += 1;
                }
                finally
                {
                    connection.Close();
                }
            }
            return itemCost;
        }
        
        public void insertAdjustments(Dictionary<string, List<Adj_div>> adjustments, RproDBHandler rproDBHandler)
        {
            foreach (KeyValuePair<string, List<Adj_div>> entry in adjustments)
            {
                List<Adj_div> adjustment = entry.Value;
                string sid = "ADJ" + entry.Key;
                using (SqlConnection connection = new SqlConnection(getConnectionString()))
                {
                    connection.Open();

                    SqlCommand command = connection.CreateCommand();
                    SqlTransaction transaction = connection.BeginTransaction("AdjustmentTransaction");
                    command.Connection = connection;
                    command.Transaction = transaction;
                    string sql = "insert into RetailPro_Adjustment(adj_sid, adj_date, storecode, subsidiary, cardcode, sign, division, totalvalue, reason, b1_status) values ";
                    sql += "(@adj_sid, @adj_date, @storecode, @subsidiary, @cardcode, @sign, @division, @totalvalue, @reason, @b1_status)";
                    command.CommandText = sql;
                    try
                    {
                        foreach (Adj_div adj_div in adjustment)
                        {
                            command.Parameters.Clear();
                            string reason = adj_div.getReason();
                            if (reason == "")
                            {
                                rproDBHandler.addLog(MainController.LogType.ERROR, sid, adj_div.division, MainController.Features.ADJUSTMENT, "Invalid creating_doc_type value " + adj_div.creating_doc_type + " in adjustment" , null);
                                continue;
                            }
                            command.Parameters.AddWithValue("adj_sid", sid);
                            command.Parameters.AddWithValue("adj_date", adj_div.adj_date);
                            command.Parameters.AddWithValue("storecode", adj_div.storecode);
                            command.Parameters.AddWithValue("subsidiary", adj_div.subsidiary);
                            command.Parameters.AddWithValue("cardcode", adj_div.cardcode);
                            command.Parameters.AddWithValue("sign", adj_div.getSign());
                            command.Parameters.AddWithValue("division", adj_div.division);
                            command.Parameters.AddWithValue("totalvalue", adj_div.totalvalue.Replace("-", ""));
                            command.Parameters.AddWithValue("reason", reason);
                            command.Parameters.AddWithValue("b1_status", adj_div.getB1_status());
                            command.ExecuteNonQuery();
                        }
                        
                        transaction.Commit();

                    }
                    catch (Exception ex)
                    {
                        rproDBHandler.addLog(MainController.LogType.EXCEPTION, sid, null, MainController.Features.ADJUSTMENT, "Exception occurred when updating adjustment table", ex);
                        AdjustmentController.error += 1;
                        // Attempt to roll back the transaction. 
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception ex2)
                        {
                            rproDBHandler.addLog(MainController.LogType.EXCEPTION, sid, null, MainController.Features.SALE, "Exception occurred when performing rollback on adjustment table ", ex2);
                        }
                    }
                }
            }
        }

        public void insertVoucherDiscrepancies(Dictionary<String, List<VoucherDiscrepancyItem>> vou_discrepancies, RproDBHandler rproDBHandler)
        {
            foreach (KeyValuePair<string, List<VoucherDiscrepancyItem>> entry in vou_discrepancies)
            {
                List<VoucherDiscrepancyItem> voucher = entry.Value;
                string sid = "VOU" + entry.Key;
                using (SqlConnection connection = new SqlConnection(getConnectionString()))
                {
                    connection.Open();

                    SqlCommand command = connection.CreateCommand();
                    SqlTransaction transaction = connection.BeginTransaction("VoucherDiscrepancyTransaction");
                    command.Connection = connection;
                    command.Transaction = transaction;
                    string sql = "insert into Retailpro_Discrepancy(vou_sid, vou_date, alu, quantity, value, sign, storeid, subsidiary, division, comments) values ";
                    sql += "(@vou_sid, @vou_date, @alu, @quantity, @value, @sign, @storeid, @subsidiary, @division, @comments)";
                    command.CommandText = sql;
                    try
                    {
                        foreach (VoucherDiscrepancyItem item in voucher)
                        {
                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("vou_sid", sid + item.getSignSingle());
                            command.Parameters.AddWithValue("vou_date", item.vou_date);
                            command.Parameters.AddWithValue("alu", item.alu);
                            command.Parameters.AddWithValue("quantity", item.getAbsoluteQty());
                            command.Parameters.AddWithValue("value", item.value);
                            command.Parameters.AddWithValue("sign", item.getSign());
                            command.Parameters.AddWithValue("storeid", item.storeid);
                            command.Parameters.AddWithValue("subsidiary", item.subsidiary);
                            command.Parameters.AddWithValue("division", item.division);
                            command.Parameters.AddWithValue("comments", item.comments);
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();

                    }
                    catch (Exception ex)
                    {
                        rproDBHandler.addLog(MainController.LogType.EXCEPTION, sid, null, MainController.Features.VOU_DISCREPANCY, "Exception occurred when updating voucher discrepancy table", ex);
                        VoucherDiscrepancyController.error += 1;
                        // Attempt to roll back the transaction. 
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception ex2)
                        {
                            rproDBHandler.addLog(MainController.LogType.EXCEPTION, sid, null, MainController.Features.VOU_DISCREPANCY, "Exception occurred when performing rollback on voucher discrepancy table ", ex2);
                        }
                    }
                }
            }
        }

        public void insertVoucherReturns(Dictionary<String, List<VoucherReturnItem>> vouchers, RproDBHandler rproDBHandler)
        {
            foreach (KeyValuePair<string, List<VoucherReturnItem>> entry in vouchers)
            {
                List<VoucherReturnItem> voucher = entry.Value;
                string sid = entry.Key;
                using (SqlConnection connection = new SqlConnection(getConnectionString()))
                {
                    connection.Open();

                    SqlCommand command = connection.CreateCommand();
                    SqlTransaction transaction = connection.BeginTransaction("VoucherReturnTransaction");
                    command.Connection = connection;
                    command.Transaction = transaction;
                    string sql = "insert into Retailpro_Return(vou_sid, vou_date, alu, quantity, linenum, storeid, subsidiary, vend_code, reason, comments) values ";
                    sql += "(@vou_sid, @vou_date, @alu, @quantity, @linenum, @storeid, @subsidiary, @vend_code, @reason, @comments)";
                    command.CommandText = sql;
                    int linenum = 0;
                    try
                    {
                        foreach (VoucherReturnItem item in voucher)
                        {
                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("vou_sid", sid);
                            command.Parameters.AddWithValue("vou_date", item.vou_date);
                            command.Parameters.AddWithValue("alu", item.alu);
                            command.Parameters.AddWithValue("quantity", item.qty);
                            command.Parameters.AddWithValue("linenum", linenum);
                            command.Parameters.AddWithValue("storeid", item.storeid);
                            command.Parameters.AddWithValue("subsidiary", item.subsidiary);
                            command.Parameters.AddWithValue("vend_code", item.vend_code);
                            command.Parameters.AddWithValue("reason", item.reason);
                            command.Parameters.AddWithValue("comments", item.comments);
                            command.ExecuteNonQuery();
                            linenum++;
                        }

                        transaction.Commit();

                    }
                    catch (Exception ex)
                    {
                        rproDBHandler.addLog(MainController.LogType.EXCEPTION, sid, null, MainController.Features.VOU_RETURN, "Exception occurred when updating voucher return table", ex);
                        VoucherReturnController.error += 1;
                        // Attempt to roll back the transaction. 
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception ex2)
                        {
                            rproDBHandler.addLog(MainController.LogType.EXCEPTION, sid, null, MainController.Features.VOU_RETURN, "Exception occurred when performing rollback on voucher return table ", ex2);
                        }
                    }
                }
            }
        }

        public void insertStoreCost(Queue<StoreCost> storecosts, RproDBHandler rproDBHandler)
        {
             
            using (SqlConnection connection = new SqlConnection(getConnectionString()))
            {
                connection.Open();
                StoreCost storecost = null;
                SqlCommand command = connection.CreateCommand();
                command.Connection = connection;
                /*
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string sql = "insert into Retailpro_StoreFullSync(storecode, subsidiary, value, post_date) values (@storecode, @subsidiary, @value, @post_date)";
                */
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd");

                command.CommandText = "update Retailpro_StoreFullSync set value = @value where storecode = @storecode and subsidiary=@subsidiary and post_date = @post_date ";
                command.CommandText += "IF @@ROWCOUNT=0 ";
                command.CommandText += "insert into Retailpro_StoreFullSync(storecode, subsidiary, value, post_date) values (@storecode, @subsidiary, @value, @post_date)";
                try
                {
                    while (storecosts.Count > 0)
                    {
                        storecost = storecosts.Dequeue();
                        if (string.IsNullOrWhiteSpace(storecost.value.Trim()))
                        {
                            storecost.value = "0";
                        }
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("storecode", storecost.storecode.Trim());
                        command.Parameters.AddWithValue("subsidiary", storecost.subsidiary.Trim());
                        command.Parameters.AddWithValue("value", storecost.value.Trim());
                        command.Parameters.AddWithValue("post_date", currentDate);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    rproDBHandler.addLog(MainController.LogType.EXCEPTION, storecost.storecode, storecost.subsidiary, MainController.Features.STORE_SYNC, "Exception occurred when updating store sync table", ex);

                }
            }
            
        }
        public bool validateStoresAndSubsidiaries(string store_no, string sbs_no)
        {
            if (sbs_no == "1" && (store_no == "001" || store_no == "009"))
                return true;
            if (sbs_no == "4" && store_no == "001")
                return true;
            return false;
        }

        public HashSet<string> getExistingSIDs(string sql, RproDBHandler rproDBHandler, MainController.Features feature)
        {
            HashSet<string> results = new HashSet<string>();
            using (SqlConnection connection = new SqlConnection(getConnectionString()))
            {
                try
                { 
                    connection.Open();
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add(reader["sid"].ToString());
                    }
                }
                catch (Exception e)
                {
                    string title = feature.ToString() + " exception occured.";
                    string body = "Exception occured when retrieving existing SIDs from B1 database. Unable to perform matching. Run aborted.";
                    rproDBHandler.addLog(MainController.LogType.EXCEPTION, null, null, feature, body, e);
                    new EmailController(settings).sendEmail(title, body, rproDBHandler, feature);
                    throw e;
                }
                finally
                {
                    connection.Close();
                }
            }
            return results;
        }
    }
}
