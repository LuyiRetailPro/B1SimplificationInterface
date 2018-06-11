using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace B1SimplificationInterface
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainController controller = new MainController();
            string[] args = Environment.GetCommandLineArgs();

           System.Console.WriteLine("system start : " + DateTime.Now);

            if (args.Length == 1)
            {
                Application.Run(new Form1(controller));
            }
            else
            {
                controller.runFromArgs(args);
            }
        }
    }
}
