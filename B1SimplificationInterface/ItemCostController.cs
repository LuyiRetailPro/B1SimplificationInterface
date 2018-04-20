using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace B1SimplificationInterface
{
    public class ItemCostController
    {
        public static int error;
        
        RproDBHandler rproDBHandler;
        MsSqlDBHandler msSqlDBHandler;
        Settings settings;
        MainController.Features feature = MainController.Features.ITEM_COST;
        public ItemCostController(Settings settings, RproDBHandler rproDBHandler, MsSqlDBHandler mySqlDBHandler)
        {
            this.settings = settings;
            this.rproDBHandler = rproDBHandler;
            this.msSqlDBHandler = mySqlDBHandler;
        }

        public Queue<CostDifference> getCostDifferences()
        {
            Dictionary<string, double> B1_costs = msSqlDBHandler.getItemCosts(rproDBHandler);
            Queue<CostDifference> updates = rproDBHandler.matchItemCost(B1_costs);
            return updates;
        }

        public void runItemCost()
        {
            error = 0;
            Dictionary<string, double> B1_costs = msSqlDBHandler.getItemCosts(rproDBHandler);
            string modified_date = rproDBHandler.getServerDate(rproDBHandler);
            if (string.IsNullOrWhiteSpace(modified_date))
            {
                return;
            }
            InventoryBO inventory = new InventoryBO(modified_date);
            Queue<CostDifference> updates = rproDBHandler.matchItemCost(B1_costs);
            foreach (CostDifference update in updates)
            {
                if (update.B1cost == 0)
                {
                    rproDBHandler.addLog(MainController.LogType.ERROR, update.item_sid, update.alu, feature, "B1 and Rpro cost mismatch - Cost in B1 is 0 when cost in Rpro is " + update.RproCost.ToString("0.0000"), null);
                }
            }
            if (updates.Count == 0)
            {
                rproDBHandler.addLog(MainController.LogType.REPORT, null, null, feature, "No XML file is created because there are no items to update", null);
                return;
            }

            while (updates.Count > 0)
            {
                CostDifference update = updates.Dequeue();
                inventory.AddInventory(update.item_sid, update.B1cost);
            }
            string filepath = createInventoryXMLFilePath();
            try
            {
                    inventory.save(filepath);
            }
            catch (Exception e)
            {
                string subject = "Exception occurred when running " + feature.ToString();
                string body = "Exception occured when trying to create Inventory.xml during Item Cost. Filepath: " + filepath;
                rproDBHandler.addLog(MainController.LogType.EXCEPTION, null, null, feature, body, e);
                new EmailController(settings).sendEmail(subject, body, rproDBHandler, feature);
                return;
            }

            string message = inventory.inventoryCount() + " items are updated in " + filepath;
            rproDBHandler.addLog(MainController.LogType.REPORT, null, null, feature, message, null);

            string command = settings.getItemCostCMDInstruction();
            string ecm = settings.getECM();
            if (!string.IsNullOrWhiteSpace(command) && !string.IsNullOrWhiteSpace(ecm))
            {
                try
                {
                    ProcessStartInfo cmdsi = new ProcessStartInfo(ecm);
                    cmdsi.Arguments = command;
                    Process cmd = Process.Start(cmdsi);
                    cmd.WaitForExit();
                    Queue<CostDifference> costDifferences = getCostDifferences();
                    if (costDifferences.Count == 0)
                    {
                        rproDBHandler.addLog(MainController.LogType.REPORT, null, null, feature, "0 cost differences after running item cost.", null);
                    }
                    else
                    {
                        rproDBHandler.addLog(MainController.LogType.ERROR, null, null, feature, costDifferences.Count + " cost differences after running item cost.", null);
                    }
                }
                catch (Exception e)
                {
                    string subject = "Exception occurred when running " + feature.ToString();
                    string body = "Exception occured when processing Inventory.xml using ECM.";

                    rproDBHandler.addLog(MainController.LogType.EXCEPTION, null, null, feature, "Exception occured when processing Inventory.xml using ECM.", e);
                    new EmailController(settings).sendEmail(subject, body, rproDBHandler, feature);
                    return;
                }

            }
        }
    
        private string createInventoryXMLFilePath()
        {
            string filepath = settings.getFilepath();
            for (int i = 1; i <= 999; i++)
            {
                string filename = "INVENTORY" + i.ToString("000") + ".XML";
                string fullpath = Path.Combine(filepath, filename);
                if (!File.Exists(fullpath))
                {
                    return fullpath;
                }
            }
            rproDBHandler.addLog(MainController.LogType.ERROR, null, null, MainController.Features.ITEM_COST, "No filename can be used to create the xml (INVENTORY001.XML to INVENTORY999.XML all in use.", null);
            return "";
        }
    }

    public class CostDifference
    {
        public string item_sid;
        public string alu;
        public double B1cost;
        public double RproCost;

        public CostDifference(string item_sid, string alu, double B1cost, double RproCost)
        {
            this.item_sid = item_sid;
            this.alu = alu;
            this.B1cost = B1cost;
            this.RproCost = RproCost;
        }
    }

    public class InventoryBO
    {
        public XmlDocument doc;
        XmlElement node;
        string modified_date;
        public InventoryBO(string modified_date)
        {
            doc = createEmptyDoc();
            this.modified_date = modified_date;
            XmlElement document = doc.CreateElement(string.Empty, "DOCUMENT", string.Empty);
            doc.AppendChild(document);
            node = doc.CreateElement(string.Empty, "INVENTORYS", string.Empty);
            document.AppendChild(node);
        }

        public bool hasInventoryItems()
        {
            return node.ChildNodes.Count != 0;
        }

        public int inventoryCount()
        {
            return node.ChildNodes.Count;
        }
        public void AddInventory(string item_sid, double cost)
        {
            XmlElement inventory = appendNode("INVENTORY", node);

            XmlElement invn = appendNode("INVN", inventory);
            invn.SetAttribute("item_sid", item_sid);

            XmlElement invn_sbs = appendNode("INVN_SBS", inventory);
            invn_sbs.SetAttribute("sbs_no", "1");
            invn_sbs.SetAttribute("modified_date", modified_date);
            invn_sbs.SetAttribute("cost", cost.ToString("0.00"));
        }

        private XmlElement appendNode(string tag_name, XmlElement parent)
        {
            XmlElement element = doc.CreateElement(string.Empty, tag_name, string.Empty);
            parent.AppendChild(element);
            return element;
        }
        private XmlDocument createEmptyDoc()
        {
            XmlDocument doc = new XmlDocument();
            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);
            return doc;
        }
        public void save(string filename)
        {
            doc.Save(filename);
        }
    }
}
