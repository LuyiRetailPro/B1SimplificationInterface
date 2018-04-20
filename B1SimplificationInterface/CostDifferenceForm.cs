using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

namespace B1SimplificationInterface
{
    public partial class CostDifferenceForm : Form
    {
        Queue<CostDifference> costDifferences;
        public CostDifferenceForm(Queue<CostDifference> costDifferences)
        {
            InitializeComponent();
            this.costDifferences = costDifferences;
            foreach (CostDifference cd in costDifferences) {
                string[] row = { cd.item_sid, cd.alu, cd.RproCost.ToString("0.00000"), cd.B1cost.ToString("0.00000")};
                listView1.Items.Add(new ListViewItem(row));
            }
            listView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void openInExcel(object sender, EventArgs e)
        {
            Microsoft.Office.Interop.Excel.Application excelApp = new Microsoft.Office.Interop.Excel.Application();
            excelApp.Workbooks.Add();
            Workbook wb = excelApp.Workbooks[1];
            excelApp.Visible = true;
            // single worksheet
            Microsoft.Office.Interop.Excel._Worksheet workSheet = wb.ActiveSheet;
            workSheet.Name = "COST DIFFERENCES";

            object[,] arr = new object[costDifferences.Count + 1, 4];
            arr[0, 0] = "Item SID";
            arr[0, 1] = "ALU";
            arr[0, 2] = "Retail Pro";
            arr[0, 3] = "B1";
            int i = 1;
            foreach (CostDifference cd in costDifferences)
            {
                arr[i, 0] = cd.item_sid;
                arr[i, 1] = cd.alu;
                arr[i, 2] = cd.RproCost.ToString("0.00");
                arr[i, 3] = cd.B1cost.ToString("0.00");
                i++;
            }

            Range c1 = (Range)workSheet.Cells[1, 1];
            Range c2 = (Range)workSheet.Cells[costDifferences.Count + 1, 4];
            Range range = workSheet.get_Range(c1, c2);
            range.set_Value(Missing.Value, arr);

            range.Columns.AutoFit();
            range.Columns["A"].NumberFormat = "0";
            range.Columns["C:D"].NumberFormat = "0.00";
        }
    }
}
