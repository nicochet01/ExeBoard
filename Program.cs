using System;
using System.Threading;
using System.Windows.Forms;

namespace ExeBoard
{
    static class Program
    {
        // Mutex para garantir instância única
        private static Mutex mutex = null;

        [STAThread]
        static void Main()
        {
            const string appName = "ExeBoard_App_Mutex_Unique_ID";
            bool createdNew;

            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // Se já existe, avisa e fecha essa nova tentativa
                MessageBox.Show("O ExeBoard já está rodando! Verifique a bandeja do sistema (perto do relógio).",
                                "Aplicação em Execução",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                return; // Encerra a execução
            }

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmCopiarExes());
        }
    }
}