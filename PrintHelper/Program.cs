using System.Drawing.Printing;

namespace PrintHelper
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            int targetX = 100, targetY = 100;
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Парсим --x= и --y=
            foreach (var arg in args)
            {
                if (arg.StartsWith("--x=")) targetX = int.Parse(arg.Split('=')[1]);
                if (arg.StartsWith("--y=")) targetY = int.Parse(arg.Split('=')[1]);
            }


            if (args.Length == 1)
            {
                PrintImage(args[0], targetX, targetY);
            }
        }

        static void PrintImage(string imagePath, int targetX, int targetY)
        {
            using var stream = File.OpenRead(imagePath);
            using var image = Image.FromStream(stream);

            var pd = new PrintDocument();
            pd.PrintPage += (sender, e) =>
            {
                e.Graphics?.DrawImage(image, e.MarginBounds);
                e.HasMorePages = false;
            };

            using var dialog = new PrintDialog
            {
                Document = pd,
                UseEXDialog = true
            };

            using var ownerForm = new Form
            {
                WindowState = FormWindowState.Minimized,
                ShowInTaskbar = false,
                Visible = false,
                Location =  new Point(targetX + 50, targetY + 50)  // ← ПОЗИЦИЯ ОТ MAUI

            };
            
                if (dialog.ShowDialog(ownerForm) == DialogResult.OK)
            {  
                pd.Print();
            }
        }
    }
}