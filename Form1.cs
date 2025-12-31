using System;
using System.Collections.Generic; // Adicionado explicitamente
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;
using System.Linq; // Adicionado explicitamente
using System.Threading; // Adicionado para Thread.Sleep
using System.Threading.Tasks; // Adicionado para Task
using Microsoft.VisualBasic;

namespace ExeBoard
{

    public partial class frmCopiarExes : Form
    {
        private void RepopularLogs(Color? filtroCor = null)
        {
            rtbLog.Clear();

            List<LogEntry> logsParaExibir;

            lock (listaDeLogs)
            {
                if (filtroCor == null)
                {
                    logsParaExibir = new List<LogEntry>(listaDeLogs);
                }
                else
                {
                    logsParaExibir = listaDeLogs.Where(log => log.Cor == filtroCor).ToList();
                }
            }

            foreach (var logEntry in logsParaExibir)
            {
                AnexarLogAoRtb(logEntry.Mensagem, logEntry.Cor);
            }
        }

        private void AnexarLogAoRtb(string mensagem, Color cor)
        {
            if (rtbLog.InvokeRequired)
            {
                rtbLog.BeginInvoke(new Action<string, Color>(AnexarLogAoRtb), mensagem, cor);
                return;
            }

            string log = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {mensagem}{Environment.NewLine}";

            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = cor;
            rtbLog.AppendText(log);
            rtbLog.SelectionColor = rtbLog.ForeColor;
            rtbLog.ScrollToCaret();
        }

        private List<LogEntry> listaDeLogs = new List<LogEntry>();

        private bool configuracoesForamAlteradas = false;

        private bool forcarFechamento = false;

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        // CORREÇÃO: Uso de AppContext.BaseDirectory para Single File
        string caminhoIni = Path.Combine(AppContext.BaseDirectory, "Inicializar.ini");

        private class LogEntry
        {
            public string Mensagem { get; set; }
            public Color Cor { get; set; }
        }

        public frmCopiarExes()
        {
            VerificarECriarIniSeNaoExistir();
            InitializeComponent();
        }

        private void VerificarECriarIniSeNaoExistir()
        {
            if (!System.IO.File.Exists(caminhoIni))
            {
                try
                {
                    // CAMINHOS PADRÃO DEFINIDOS AQUI
                    string conteudoPadrao =
                        "[CONFIG_GERAIS]\n" +
                        "RODAR_NA_BANDEJA=Sim\n\n" +
                        "[CAMINHOS]\n" +
                        "DE=\n" +
                        "DE_PASTA_CLIENT=EXES\n" +
                        "DE_PASTA_SERVER=EXES\n" +
                        "DE_PASTA_DADOS=BD\n" +
                        "PARA=C:\\\n" + // Raiz padrão
                        "PASTA_CLIENT=C:\\Viasoft\\Client\n" + // Padrão solicitado
                        "PASTA_SERVER=C:\\Viasoft\\Server\n" + // Padrão solicitado
                        "PASTA_DADOS=C:\\Viasoft\\Dados\n\n" + // Padrão solicitado
                        "[APLICACOES_CLIENTE]\n" +
                        "Count=0\n\n" +
                        "[APLICACOES_SERVIDORAS]\n" +
                        "Count=0\n\n" +
                        "[BANCO_DE_DADOS]\n" +
                        "Count=0\n\n" +
                        "[ATUALIZADORES]\n" +
                        "Count=0\n" +
                        "[ULTIMOS_CAMINHOS]\n" +
                        "UltimoCaminhoClientes=C:\\Viasoft\\Client\n" +
                        "UltimoCaminhoServidores=C:\\Viasoft\\Server\n" +
                        "UltimoCaminhoAtualizadores=C:\\Viasoft\\Dados\n";

                    System.IO.File.WriteAllText(caminhoIni, conteudoPadrao);

                    // Aviso de Boas-vindas (UX)
                    MessageBox.Show("Configuração inicial criada com os caminhos padrão da Viasoft.\n\nVerifique se as pastas C:\\Viasoft... existem na sua máquina.",
                                    "Bem-vindo ao ExeBoard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro crítico ao criar INI: {ex.Message}", "Erro Fatal", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }
            }
        }

        private void CarregarDadosDeConfiguracao()
        {
            clbClientes.Items.Clear();
            clbServidores.Items.Clear();
            clbAtualizadores.Items.Clear();

            clbClientes.DisplayMember = "Nome";
            clbServidores.DisplayMember = "Nome";

            RegistrarLogCopiarDados("Carregando dados na aba de configurações...");

            string countClientesStr = LerValorIni("APLICACOES_CLIENTE", "Count", caminhoIni);
            if (int.TryParse(countClientesStr, out int countClientes))
            {
                for (int i = 0; i < countClientes; i++)
                {
                    string clienteNome = LerValorIni("APLICACOES_CLIENTE", $"Cliente{i}", caminhoIni);
                    string categoria = LerValorIni("APLICACOES_CLIENTE", $"Categoria{i}", caminhoIni);
                    if (!string.IsNullOrWhiteSpace(clienteNome))
                    {
                        clbClientes.Items.Add(new ClienteItem { Nome = clienteNome, Categoria = categoria });
                    }
                }
            }

            string countServidoresStr = LerValorIni("APLICACOES_SERVIDORAS", "Count", caminhoIni);
            if (int.TryParse(countServidoresStr, out int countServidores))
            {
                for (int i = 0; i < countServidores; i++)
                {
                    string servidorNome = LerValorIni("APLICACOES_SERVIDORAS", $"Servidor{i}", caminhoIni);
                    string tipo = LerValorIni("APLICACOES_SERVIDORAS", $"Tipo{i}", caminhoIni);
                    string replicarStr = LerValorIni("APLICACOES_SERVIDORAS", $"Replicar{i}", caminhoIni);
                    bool replicar = string.Equals(replicarStr, "Sim", StringComparison.OrdinalIgnoreCase);

                    if (!string.IsNullOrWhiteSpace(servidorNome))
                    {
                        clbServidores.Items.Add(new ServidorItem { Nome = servidorNome, Tipo = tipo, ReplicarParaCopia = replicar });
                    }
                }
            }

            string countBancosStr = LerValorIni("BANCO_DE_DADOS", "Count", caminhoIni);
            if (int.TryParse(countBancosStr, out int countBancos))
            {
                for (int i = 0; i < countBancos; i++)
                {
                    string bancoNome = LerValorIni("BANCO_DE_DADOS", $"Banco{i}", caminhoIni);
                    if (string.IsNullOrWhiteSpace(bancoNome))
                    {
                        bancoNome = LerValorIni("BANCO_DE_DADOS", $"BancoDados{i}", caminhoIni);
                    }

                    if (!string.IsNullOrWhiteSpace(bancoNome))
                    {
                        clbAtualizadores.Items.Add(bancoNome);
                    }
                }
            }

            configuracoesForamAlteradas = false;
            RegistrarLogCopiarDados("Dados de configuração carregados.");
            AtualizarEstadoBotoesConfig();
        }
        private void btnBuscarCaminhoBranch_Click(object sender, EventArgs e)
        {
            // CORREÇÃO BUG 1: Usar OpenFileDialog para o usuário VER os arquivos
            using (OpenFileDialog dialogo = new OpenFileDialog())
            {
                dialogo.Title = "Selecione qualquer arquivo dentro da pasta da Branch";
                dialogo.Filter = "Todos os Arquivos (*.*)|*.*";
                dialogo.CheckFileExists = true;

                // Tenta iniciar onde já está configurado
                string caminhoInicial = edtCaminhoBranch.Text;
                if (!string.IsNullOrWhiteSpace(caminhoInicial) && Directory.Exists(caminhoInicial))
                {
                    dialogo.InitialDirectory = caminhoInicial;
                }

                if (dialogo.ShowDialog() == DialogResult.OK)
                {
                    // O pulo do gato: Pegamos o diretório onde o arquivo está
                    string pastaSelecionada = Path.GetDirectoryName(dialogo.FileName);

                    edtCaminhoBranch.Text = pastaSelecionada;
                    WritePrivateProfileString("CAMINHOS", "DE", pastaSelecionada, caminhoIni);
                    SalvarCaminhosDaTela();
                }
            }
        }
        // Adicione esta variável no topo da classe (junto com os outros controles ou variáveis globais)
        private CheckBox cbModoAutomacao;

        private void frmCopiarExes_Load(object sender, EventArgs e)
        {
            this.Location = new Point(this.Location.X, 0);
            RegistrarLogCopiarDados("CopiarExes aberto");

            edtCaminhoBranch.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            edtCaminhoBranch.AutoCompleteSource = AutoCompleteSource.FileSystemDirectories;

            preencheAutomaticamenteOCampoDe();

            // --- CORREÇÃO BUG 2: BANDEJA (Garantir Ícone) ---
            // Se o Form não tiver ícone, o NotifyIcon não aparece. Usamos um padrão do sistema se falhar.
            if (this.Icon != null)
                icBandeja.Icon = this.Icon;
            else
                icBandeja.Icon = SystemIcons.Application;

            icBandeja.Text = "ExeBoard - Gerenciador";

            // Configura o menu da bandeja
            ContextMenuStrip menuBandeja = new ContextMenuStrip();
            menuBandeja.Items.Add("Abrir Painel", null, (s, ev) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.BringToFront(); // Traz para frente
            });
            menuBandeja.Items.Add("-");
            menuBandeja.Items.Add("Fechar ExeBoard", null, (s, ev) => {
                forcarFechamento = true;
                Application.Exit();
            });
            icBandeja.ContextMenuStrip = menuBandeja;

            // --- MELHORIA: CHECKBOX AUTOMAÇÃO (Criado via código) ---
            cbModoAutomacao = new CheckBox();
            cbModoAutomacao.Text = "Usar Executáveis de Automação (ExesAutomacao)";
            cbModoAutomacao.AutoSize = true;
            cbModoAutomacao.Location = new Point(edtCaminhoBranch.Location.X, edtCaminhoBranch.Bottom + 5);
            cbModoAutomacao.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            cbModoAutomacao.ForeColor = Color.DarkBlue;

            // Adiciona na aba "Copiar Dados" -> GroupBox Branch
            gbBranch.Controls.Add(cbModoAutomacao);
            // Aumenta um pouco o GroupBox para caber
            gbBranch.Height += 25;

            // --- Carregamento dos Caminhos ---
            ConfigurarCaminhosPadraoSeVazio(txtDestinoAtualizadores, @"C:\Viasoft\Dados", "PASTA_DADOS", "UltimoCaminhoAtualizadores");
            ConfigurarCaminhosPadraoSeVazio(txtDestinoClientes, @"C:\Viasoft\Client", "PASTA_CLIENT", "UltimoCaminhoClientes");
            ConfigurarCaminhosPadraoSeVazio(txtDestinoServidores, @"C:\Viasoft\Server", "PASTA_SERVER", "UltimoCaminhoServidores");

            // Carregamento de Listas
            CarregarBancoDeDados();
            CarregarClientes();
            CarregarServidores();
            carregarColaboradores();

            // Verifica se deve rodar na bandeja ao iniciar
            string rodarNaBandeja = LerValorIni("CONFIG_GERAIS", "RODAR_NA_BANDEJA", this.caminhoIni);
            if (rodarNaBandeja == "Sim")
            {
                icBandeja.Visible = true;
            }

            // Ícones dos botões de busca
            try
            {
                string caminhoIcone = Path.Combine(AppContext.BaseDirectory, "search.png");
                if (File.Exists(caminhoIcone))
                {
                    Image iconeLupa = Image.FromFile(caminhoIcone);
                    btnProcurarAtualizadores.Image = iconeLupa;
                    btnProcurarClientes.Image = iconeLupa;
                    btnProcurarServidores.Image = iconeLupa;
                }
            }
            catch { }
        }
        private void ReiniciarServidor(ServidorItem servidor, bool acionadoNaBandeja)
        {
            if (servidor == null) return;

            string caminhoServidorDestino = txtDestinoServidores.Text;
            string pastaServerIni = LerValorIni("CAMINHOS", "PASTA_SERVER", caminhoIni);

            if (servidor.Tipo == "Servico")
            {
                try
                {
                    ServiceController sc = new ServiceController(servidor.Nome);
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        RegistrarLogCopiarDados("Parou o serviço " + servidor.Nome);
                    }
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    RegistrarLogCopiarDados("Reiniciou o serviço " + servidor.Nome);
                    if (acionadoNaBandeja) MessageBox.Show($"Serviço {servidor.Nome} reiniciado.", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    RegistrarLogCopiarDados($"Erro ao reiniciar serviço {servidor.Nome}: {ex.Message}");
                    if (acionadoNaBandeja) MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (servidor.Tipo == "Aplicacao")
            {
                try
                {
                    string caminhoCompletoExeDestino = Path.Combine(caminhoServidorDestino, pastaServerIni, servidor.SubDiretorios ?? "", servidor.Nome);
                    string nomeProcesso = Path.GetFileNameWithoutExtension(servidor.Nome);

                    foreach (var processo in Process.GetProcessesByName(nomeProcesso))
                    {
                        processo.Kill();
                        processo.WaitForExit();
                        RegistrarLogCopiarDados("Parou a aplicação: " + servidor.Nome);
                    }
                    Process.Start(new ProcessStartInfo { FileName = caminhoCompletoExeDestino, UseShellExecute = true });
                    RegistrarLogCopiarDados("Reiniciou a aplicação: " + servidor.Nome);
                    if (acionadoNaBandeja) MessageBox.Show($"Aplicação {servidor.Nome} reiniciada.", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    RegistrarLogCopiarDados($"Erro ao reiniciar aplicação {servidor.Nome}: {ex.Message}");
                    if (acionadoNaBandeja) MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PararServidor(ServidorItem servidor, bool acionadoNaBandeja, bool validarParaCopia = false)
        {
            if (servidor == null) return;

            string nomeProcesso = Path.GetFileNameWithoutExtension(servidor.Nome);

            // ---------------------------------------------------------
            // ETAPA 1: Tenta parar via ServiceController (O jeito educado)
            // ---------------------------------------------------------
            if (servidor.Tipo == "Servico")
            {
                try
                {
                    ServiceController sc = new ServiceController(servidor.Nome);
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                    {
                        RegistrarLogCopiarDados($"Parando o serviço {servidor.Nome}...");
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        RegistrarLogCopiarDados($"Serviço {servidor.Nome} reportou status 'Parado'.");
                    }
                }
                catch (Exception ex)
                {
                    RegistrarLogCopiarDados($"Aviso ao parar serviço {servidor.Nome}: {ex.Message} (Tentaremos forçar o encerramento do processo)");
                }
            }

            // ---------------------------------------------------------
            // ETAPA 2: O "Tiro de Misericórdia" (Garante que o EXE morreu)
            // Serve tanto para Aplicação quanto para Serviço que travou
            // ---------------------------------------------------------
            try
            {
                var processos = Process.GetProcessesByName(nomeProcesso);
                if (processos.Length > 0)
                {
                    RegistrarLogCopiarDados($"Detectado(s) {processos.Length} processo(s) de {nomeProcesso} ainda ativos. Forçando encerramento...");
                    foreach (var p in processos)
                    {
                        try
                        {
                            p.Kill(); // Mata o processo
                            p.WaitForExit(3000); // Espera até 3s para ter certeza que morreu
                        }
                        catch { /* Ignora erro de acesso se já morreu */ }
                    }
                    RegistrarLogCopiarDados($"Processo {nomeProcesso} encerrado com força bruta.");
                }
            }
            catch (Exception ex)
            {
                RegistrarLogCopiarDados($"Erro ao tentar matar processo {nomeProcesso}: {ex.Message}");
            }

            // ---------------------------------------------------------
            // ETAPA 3: Validação Final (Só se for copiar arquivos)
            // ---------------------------------------------------------
            if (validarParaCopia)
            {
                RegistrarLogCopiarDados($"Validando liberação de {servidor.Nome}...");
                bool liberado = false;

                // Tenta verificar se o processo sumiu da lista por 10 segundos
                for (int i = 0; i < 10; i++)
                {
                    if (Process.GetProcessesByName(nomeProcesso).Length == 0)
                    {
                        liberado = true;
                        break;
                    }
                    Thread.Sleep(1000);
                }

                if (liberado)
                {
                    RegistrarLogCopiarDados($"OK: {servidor.Nome} totalmente encerrado.");
                }
                else
                {
                    RegistrarLogCopiarDados($"ERRO CRÍTICO: {servidor.Nome} ainda está rodando e vai bloquear a cópia.", Color.Red);
                    // Não vamos lançar Exception aqui para não parar o fluxo dos outros, 
                    // mas o método de cópia vai tentar e falhar se ainda estiver preso.
                }
            }
        }
        private void preencheAutomaticamenteOCampoDe()
        {
            string valorDE = LerValorIni("CAMINHOS", "DE", this.caminhoIni);
            if (Directory.Exists(valorDE))
            {
                edtCaminhoBranch.Text = valorDE;
            }
            else
            {
                RegistrarLogCopiarDados("Não encontrou o arquivo Inicializar.ini ou parâmetro DE não existe.");
            }
        }

        private void CarregarClientes()
        {
            cbGroupClientes.Items.Clear();
            List<ClienteItem> listaTemporaria = new List<ClienteItem>(); // Lista para ordenar

            string countStr = LerValorIni("APLICACOES_CLIENTE", "Count", caminhoIni);
            if (int.TryParse(countStr, out int count))
            {
                for (int i = 0; i < count; i++)
                {
                    string cliente = LerValorIni("APLICACOES_CLIENTE", $"Cliente{i}", caminhoIni);
                    string categoria = LerValorIni("APLICACOES_CLIENTE", $"Categoria{i}", caminhoIni);
                    string subDiretorio = LerValorIni("APLICACOES_CLIENTE", $"SubDiretorios{i}", caminhoIni);

                    if (!string.IsNullOrWhiteSpace(cliente))
                    {
                        listaTemporaria.Add(new ClienteItem
                        {
                            Nome = cliente,
                            Categoria = categoria,
                            SubDiretorios = subDiretorio
                        });
                    }
                }
            }

            // TAREFA 6: Ordenação Alfabética
            var listaOrdenada = listaTemporaria.OrderBy(c => c.Nome).ToList();

            foreach (var item in listaOrdenada)
            {
                cbGroupClientes.Items.Add(item);
                cbGroupClientes.SetItemChecked(cbGroupClientes.Items.Count - 1, true);
            }
        }

        private void CarregarServidores()
        {
            cbGroupServidores.Items.Clear();
            List<ServidorItem> listaTemporaria = new List<ServidorItem>();

            string countStr = LerValorIni("APLICACOES_SERVIDORAS", "Count", caminhoIni);
            if (int.TryParse(countStr, out int count))
            {
                for (int i = 0; i < count; i++)
                {
                    string servidor = LerValorIni("APLICACOES_SERVIDORAS", $"Servidor{i}", caminhoIni);
                    string tipo = LerValorIni("APLICACOES_SERVIDORAS", $"Tipo{i}", caminhoIni);
                    string subDir = LerValorIni("APLICACOES_SERVIDORAS", $"SubDiretorios{i}", caminhoIni);
                    string replicar = LerValorIni("APLICACOES_SERVIDORAS", $"Replicar{i}", caminhoIni);

                    if (!string.IsNullOrWhiteSpace(servidor))
                    {
                        // Só adiciona se for para replicar (lógica original)
                        if (string.Equals(replicar, "Sim", StringComparison.OrdinalIgnoreCase))
                        {
                            listaTemporaria.Add(new ServidorItem
                            {
                                Nome = servidor,
                                Tipo = tipo,
                                ReplicarParaCopia = true,
                                SubDiretorios = subDir
                            });
                        }
                    }
                }
            }

            // TAREFA 6: Ordenação Alfabética
            var listaOrdenada = listaTemporaria.OrderBy(s => s.Nome).ToList();

            foreach (var item in listaOrdenada)
            {
                cbGroupServidores.Items.Add(item);
                cbGroupServidores.SetItemChecked(cbGroupServidores.Items.Count - 1, true);
            }
        }

        private void CarregarBancoDeDados()
        {
            cbGroupAtualizadores.Items.Clear();
            List<string> listaTemporaria = new List<string>();

            string countStr = LerValorIni("BANCO_DE_DADOS", "Count", caminhoIni);
            if (int.TryParse(countStr, out int count))
            {
                for (int i = 0; i < count; i++)
                {
                    string valor = LerValorIni("BANCO_DE_DADOS", $"Banco{i}", caminhoIni);
                    if (string.IsNullOrWhiteSpace(valor)) valor = LerValorIni("BANCO_DE_DADOS", $"BancoDados{i}", caminhoIni);

                    if (!string.IsNullOrWhiteSpace(valor))
                    {
                        listaTemporaria.Add(valor);
                    }
                }
            }

            // TAREFA 6: Ordenação Alfabética
            listaTemporaria.Sort();

            foreach (var item in listaTemporaria)
            {
                cbGroupAtualizadores.Items.Add(item);
                cbGroupAtualizadores.SetItemChecked(cbGroupAtualizadores.Items.Count - 1, true);
            }
        }

        // --------------------------------------------------------------------------------
        // MÉTODO 1: O Loop (iniciarServidores - Plural)
        // Esse é o que estava faltando (Erro CS0103)
        // --------------------------------------------------------------------------------
        private void iniciarServidores(string caminhoServidorDestino, List<ServidorItem> servidoresParaIniciar)
        {
            foreach (var servidor in servidoresParaIniciar)
            {
                // Chama o método individual abaixo
                IniciarServidor(servidor, false);
            }

            RegistrarLogCopiarDados("Sistema pronto para uso...");
            RegistrarLogCopiarDados("Se necessário, atualize o banco de dados a ser utilizado...");
        }

        // --------------------------------------------------------------------------------
        // MÉTODO 2: A Lógica Individual (IniciarServidor - Singular)
        // Esse é o que estava duplicado (Erro CS0111). Agora só teremos este.
        // --------------------------------------------------------------------------------
        private void IniciarServidor(ServidorItem servidor, bool acionadoNaBandeja)
        {
            if (servidor == null) return;

            string caminhoServidorDestino = txtDestinoServidores.Text; // Pega o texto da tela

            if (servidor.Tipo == "Servico")
            {
                try
                {
                    ServiceController sc = new ServiceController(servidor.Nome);
                    sc.Refresh(); // Atualiza status

                    if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                    {
                        RegistrarLogCopiarDados($"Solicitando início do serviço {servidor.Nome}...");
                        sc.Start();

                        // Aguarda até 1 minuto (Correção do Timeout)
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1));

                        RegistrarLogCopiarDados("Iniciou o serviço " + servidor.Nome, Color.DarkGreen);
                        if (acionadoNaBandeja) MessageBox.Show($"Serviço {servidor.Nome} iniciado.", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (acionadoNaBandeja)
                    {
                        MessageBox.Show($"Serviço {servidor.Nome} já estava iniciado.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    RegistrarLogCopiarDados($"ALERTA: O serviço {servidor.Nome} demorou mais de 1 minuto para responder.", Color.Orange);
                }
                catch (Exception ex)
                {
                    RegistrarLogCopiarDados($"Erro ao iniciar serviço {servidor.Nome}: {ex.Message}", Color.Red);
                    if (acionadoNaBandeja) MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (servidor.Tipo == "Aplicacao")
            {
                try
                {
                    // Tenta achar onde está o executável para iniciar
                    // Usa o caminho da tela + subdiretórios
                    string pastaServerIni = LerValorIni("CAMINHOS", "PASTA_SERVER", caminhoIni); // Backup caso a tela esteja vazia
                    string baseDir = !string.IsNullOrWhiteSpace(caminhoServidorDestino) ? caminhoServidorDestino : pastaServerIni;

                    string caminhoCompletoExeDestino = Path.Combine(baseDir, servidor.SubDiretorios ?? "", servidor.Nome);

                    // Verifica se o arquivo existe antes de tentar abrir
                    if (!File.Exists(caminhoCompletoExeDestino))
                    {
                        // Tenta achar só com o nome se o caminho completo falhar
                        caminhoCompletoExeDestino = Path.Combine(baseDir, servidor.Nome);
                    }

                    Process.Start(new ProcessStartInfo { FileName = caminhoCompletoExeDestino, UseShellExecute = true });
                    RegistrarLogCopiarDados("Iniciou a aplicação: " + servidor.Nome);

                    if (acionadoNaBandeja) MessageBox.Show($"Aplicação {servidor.Nome} iniciada.", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    RegistrarLogCopiarDados($"Erro ao iniciar aplicação {servidor.Nome}: {ex.Message}", Color.Red);
                    if (acionadoNaBandeja) MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string LerValorIni(string secao, string chave, string caminhoArquivo)
        {
            StringBuilder buffer = new StringBuilder(255);
            GetPrivateProfileString(secao, chave, "", buffer, buffer.Capacity, caminhoArquivo);
            return buffer.ToString();
        }

        private void RegistrarLogCopiarDados(string mensagem, Color cor)
        {
            var logEntry = new LogEntry { Mensagem = mensagem, Cor = cor };

            lock (listaDeLogs)
            {
                listaDeLogs.Add(logEntry);
            }

            AnexarLogAoRtb(mensagem, cor);
        }
        private void RegistrarLogCopiarDados(string mensagem)
        {
            RegistrarLogCopiarDados(mensagem, Color.Black);
        }


        private void RegistrarLogServidores(string mensagem, Color cor)
        {
            if (rtbLogServidores.InvokeRequired)
            {
                rtbLogServidores.BeginInvoke(new Action<string, Color>(RegistrarLogServidores), mensagem, cor);
                return;
            }

            string log = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {mensagem}{Environment.NewLine}";

            rtbLogServidores.SelectionStart = rtbLogServidores.TextLength;
            rtbLogServidores.SelectionLength = 0;
            rtbLogServidores.SelectionColor = cor;
            rtbLogServidores.AppendText(log);
            rtbLogServidores.SelectionColor = rtbLogServidores.ForeColor;
            rtbLogServidores.ScrollToCaret();
        }

        private void RegistrarLogServidores(string mensagem)
        {
            RegistrarLogServidores(mensagem, Color.Black);
        }

        private async void btnCopiarDados_Click(object sender, EventArgs e)
        {
            SalvarCaminhosDaTela();
            btnCopiarDados.Enabled = false;
            RegistrarLogCopiarDados("Iniciando processo de cópia...", Color.Blue);

            string caminhoBranch = edtCaminhoBranch.Text;

            // --- LÓGICA DA MELHORIA (AUTOMAÇÃO) ---
            string pastaOrigemExecutaveis = "";

            if (cbModoAutomacao.Checked)
            {
                // Se marcado, força a pasta de automação
                pastaOrigemExecutaveis = Path.Combine(caminhoBranch, "ExesAutomacao");
                RegistrarLogCopiarDados("MODO AUTOMAÇÃO ATIVO: Buscando em 'ExesAutomacao'", Color.Purple);
            }
            else
            {
                // Se desmarcado, usa o padrão (Exes) ou o que estiver no INI
                string pastaClientIni = LerValorIni("CAMINHOS", "DE_PASTA_CLIENT", caminhoIni);
                string subPastaPadrao = !string.IsNullOrWhiteSpace(pastaClientIni) ? pastaClientIni : "Exes"; // Default "Exes"
                pastaOrigemExecutaveis = Path.Combine(caminhoBranch, subPastaPadrao);
            }

            // Verifica se a pasta de origem existe
            if (!Directory.Exists(pastaOrigemExecutaveis))
            {
                RegistrarLogCopiarDados($"ERRO: A pasta de origem não existe: {pastaOrigemExecutaveis}", Color.Red);
                // Tenta fallback para a raiz se falhar
                RegistrarLogCopiarDados("Tentando buscar na raiz da Branch...", Color.Orange);
                pastaOrigemExecutaveis = caminhoBranch;
            }
            // --------------------------------------

            string caminhoClienteDestino = txtDestinoClientes.Text;
            string caminhoServidorDestino = txtDestinoServidores.Text;
            string caminhoAtualizadores = txtDestinoAtualizadores.Text;

            var clientesParaCopiar = cbGroupClientes.CheckedItems.OfType<ClienteItem>().ToList();
            var servidoresParaCopiar = cbGroupServidores.CheckedItems.OfType<ServidorItem>().ToList();
            var atualizadoresParaCopiar = cbGroupAtualizadores.CheckedItems.Cast<object>().ToList();

            try
            {
                bool sucesso = await Task.Run(() =>
                {
                    RegistrarLogCopiarDados("Parando aplicações clientes...");
                    if (!encerrarClientes(caminhoClienteDestino, clientesParaCopiar))
                    {
                        RegistrarLogCopiarDados("ERRO: Falha ao encerrar clientes. Abortando.", Color.Red);
                        return false;
                    }

                    RegistrarLogCopiarDados("Parando aplicações servidoras...");
                    if (!encerrarServidores(servidoresParaCopiar))
                    {
                        RegistrarLogCopiarDados("ERRO: Falha ao encerrar servidores. Abortando.", Color.Red);
                        return false;
                    }

                    RegistrarLogCopiarDados("Copiando executáveis via C#...");

                    // ATENÇÃO: Passamos agora 'pastaOrigemExecutaveis' em vez de 'caminhoBranch' puro
                    // para evitar que ele indexe a pasta errada.
                    copiarArquivos(pastaOrigemExecutaveis, caminhoBranch, caminhoClienteDestino, caminhoServidorDestino, caminhoAtualizadores,
                                   clientesParaCopiar, servidoresParaCopiar, atualizadoresParaCopiar);

                    RegistrarLogCopiarDados("Iniciando servidores...");
                    iniciarServidores(caminhoServidorDestino, servidoresParaCopiar);

                    return true;
                });

                if (sucesso)
                    RegistrarLogCopiarDados("Processo de cópia concluído!", Color.Blue);
                else
                    RegistrarLogCopiarDados("Processo de cópia falhou. Verifique os logs.", Color.Red);
            }
            catch (Exception ex)
            {
                RegistrarLogCopiarDados($"ERRO FATAL no processo de cópia: {ex.Message}", Color.Red);
            }
            finally
            {
                btnCopiarDados.Enabled = true;
            }
        }
        private bool encerrarServidores(List<ServidorItem> servidoresParaParar)
        {
            try
            {
                foreach (var servidor in servidoresParaParar)
                {
                    PararServidor(servidor, false, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                RegistrarLogCopiarDados($"FALHA: {ex.Message}");
                return false;
            }
        }
        private bool encerrarClientes(string caminhoClienteDestino, List<ClienteItem> clientesParaParar)
        {
            // CORREÇÃO: O loop estava vazio! Agora ele chama a função para matar o processo.
            foreach (var cliente in clientesParaParar)
            {
                // Convertemos o ClienteItem para ServidorItem temporariamente só para usar a lógica de 'PararServidor'
                // ou implementamos o Kill direto aqui. Vamos usar o Kill direto para ser mais simples:
                try
                {
                    string nomeProcesso = Path.GetFileNameWithoutExtension(cliente.Nome);
                    foreach (var p in Process.GetProcessesByName(nomeProcesso))
                    {
                        p.Kill();
                        p.WaitForExit(2000);
                    }
                }
                catch { /* Ignora se já morreu */ }
            }

            RegistrarLogCopiarDados("OK: Clientes encerrados. Excluindo arquivos antigos...");

            foreach (var cliente in clientesParaParar)
            {
                try
                {
                    string caminhoCompletoExeDestino = Path.Combine(caminhoClienteDestino, cliente.SubDiretorios ?? "", cliente.Nome);
                    if (File.Exists(caminhoCompletoExeDestino))
                    {
                        File.Delete(caminhoCompletoExeDestino);
                        RegistrarLogCopiarDados($"OK: Arquivo antigo {cliente.Nome} excluído.");
                    }
                }
                catch (Exception ex)
                {
                    RegistrarLogCopiarDados($"ERRO ao excluir {cliente.Nome}: {ex.Message}");
                }
            }
            return true;
        }
        private string EncontrarArquivoNaOrigem(string diretorioRaiz, string nomeArquivo)
        {
            // 1. Tenta achar direto na raiz
            string tentativaDireta = Path.Combine(diretorioRaiz, nomeArquivo);
            if (File.Exists(tentativaDireta)) return tentativaDireta;

            // 2. Se não achar, varre todas as subpastas (Recursivo)
            try
            {
                // SearchOption.AllDirectories faz a mágica de descer em todas as pastas
                var arquivosEncontrados = Directory.GetFiles(diretorioRaiz, nomeArquivo, SearchOption.AllDirectories);

                // Retorna o primeiro que encontrar, ou null se não achar nada
                return arquivosEncontrados.FirstOrDefault();
            }
            catch (UnauthorizedAccessException)
            {
                // Ignora pastas que não temos permissão para ler
                return null;
            }
        }

        // -------------------------------------------------------------------------
        // NOVO MÉTODO AUXILIAR: Indexa todos os arquivos de uma pasta (Super Rápido)
        // -------------------------------------------------------------------------
        private Dictionary<string, string> IndexarDiretorio(string diretorioRaiz)
        {
            Dictionary<string, string> mapaArquivos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(diretorioRaiz))
                return mapaArquivos;

            try
            {
                // Pega TODOS os arquivos de uma vez só (muito mais rápido que buscar um por um)
                // Usamos SearchOption.AllDirectories para descer em todas as subpastas
                string[] arquivos = Directory.GetFiles(diretorioRaiz, "*.*", SearchOption.AllDirectories);

                foreach (string arquivo in arquivos)
                {
                    string nomeArquivo = Path.GetFileName(arquivo);

                    // Se houver duplicados, mantemos o primeiro encontrado ou o mais raso
                    if (!mapaArquivos.ContainsKey(nomeArquivo))
                    {
                        mapaArquivos.Add(nomeArquivo, arquivo);
                    }
                }
            }
            catch (Exception ex)
            {
                RegistrarLogCopiarDados($"AVISO: Não foi possível indexar totalmente a pasta {diretorioRaiz}. Erro: {ex.Message}", Color.Orange);
            }

            return mapaArquivos;
        }

        // -------------------------------------------------------------------------
        // MÉTODO DE CÓPIA OTIMIZADO (Substitua o antigo 'copiarArquivos' por este)
        // -------------------------------------------------------------------------
        // Mude a assinatura para receber 'caminhoRaizBranch' separado de 'caminhoOrigemExes'
        private void copiarArquivos(string caminhoOrigemExes, string caminhoRaizBranch, string paraClientes, string paraServidores, string paraAtualizadores,
                                    List<ClienteItem> clientesParaCopiar, List<ServidorItem> servidoresParaCopiar, List<object> atualizadoresParaCopiar)
        {
            string dePastaDados = LerValorIni("CAMINHOS", "DE_PASTA_DADOS", caminhoIni);
            bool houveAprendizado = false;

            RegistrarLogCopiarDados("------------------------------------------------");
            RegistrarLogCopiarDados($"Origem dos Executáveis: {Path.GetFileName(caminhoOrigemExes)}", Color.Blue);

            // Indexa a pasta ESPECÍFICA (Exes ou ExesAutomacao)
            var mapaOrigem = IndexarDiretorio(caminhoOrigemExes);

            var mapaDestinoClientes = IndexarDiretorio(paraClientes);
            var mapaDestinoServidores = IndexarDiretorio(paraServidores);

            // ... (O resto da lógica de Clientes e Servidores continua igual, pois usam o 'mapaOrigem') ...
            // ...
            // ...

            // --- CORREÇÃO NA CÓPIA DE DADOS (ATUALIZADORES) ---
            // Os atualizadores (BD) geralmente não estão dentro de 'Exes', estão na raiz ou em 'BD'.
            // Então usamos 'caminhoRaizBranch' aqui.
            foreach (var item in atualizadoresParaCopiar)
            {
                string nomePasta = item.ToString();

                // Tenta achar em Branch/BD/NomePasta
                string sourceDir = Path.Combine(caminhoRaizBranch, dePastaDados, nomePasta);

                // Se não achar, tenta em Branch/NomePasta
                if (!Directory.Exists(sourceDir)) sourceDir = Path.Combine(caminhoRaizBranch, nomePasta);

                string destinationDir = Path.Combine(paraAtualizadores, nomePasta);
                CopiarDiretorioComLog(sourceDir, destinationDir, nomePasta);
            }

            // ... (Resto do código igual) ...
        }
        private void CopiarArquivoComLog(string sourcePath, string destinationPath, string destinationDir, string nomeArquivo)
        {
            int tentativas = 0;
            int maxTentativas = 5;
            bool sucesso = false;

            while (tentativas < maxTentativas && !sucesso)
            {
                try
                {
                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    // PREVENÇÃO DE ERRO: Garante que o arquivo destino não esteja como 'Somente Leitura'
                    if (File.Exists(destinationPath))
                    {
                        FileInfo fi = new FileInfo(destinationPath);
                        if (fi.IsReadOnly)
                        {
                            fi.IsReadOnly = false;
                        }
                    }

                    File.Copy(sourcePath, destinationPath, true);
                    sucesso = true; // Se passar daqui, deu certo

                    // Pinta de verde (sucesso)
                    RegistrarLogCopiarDados($"OK: {nomeArquivo} copiado para {Path.GetFileName(destinationDir)}", Color.DarkGreen);
                }
                catch (IOException)
                {
                    // Erro de arquivo em uso (o famoso 'Função Incorreta' ou 'Arquivo em uso')
                    tentativas++;
                    RegistrarLogCopiarDados($"   -> Arquivo {nomeArquivo} em uso ou bloqueado. Tentativa {tentativas}/{maxTentativas}...", Color.Orange);
                    Thread.Sleep(2000); // Espera 2 segundos antes de tentar de novo
                }
                catch (UnauthorizedAccessException)
                {
                    RegistrarLogCopiarDados($"ERRO: Sem permissão para acessar {destinationPath}. Tentando liberar...", Color.Red);
                    // Tenta remover atributos e tenta de novo na próxima volta do loop
                    try
                    {
                        new FileInfo(destinationPath).Attributes = FileAttributes.Normal;
                    }
                    catch { }
                    tentativas++;
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    RegistrarLogCopiarDados($"ERRO FATAL ao copiar {nomeArquivo}: {ex.Message}", Color.Red);
                    break; // Erro desconhecido, aborta este arquivo
                }
            }

            if (!sucesso)
            {
                RegistrarLogCopiarDados($"FALHA FINAL: Não foi possível copiar {nomeArquivo} após {maxTentativas} tentativas.", Color.Red);
            }
        }
        protected override void OnResize(EventArgs e)
        {
            string rodarNaBandeja = LerValorIni("CONFIG_GERAIS", "RODAR_NA_BANDEJA", this.caminhoIni);

            if (rodarNaBandeja == "Sim")
            {

                base.OnResize(e);

                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.Hide();
                    icBandeja.Visible = true;
                }
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            icBandeja.Visible = false;
        }

        private void frmCopiarExes_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Se clicou em "Fechar" no menu da bandeja, fecha direto
            if (forcarFechamento) return;

            // Senão, aplica a lógica de minimizar para a bandeja
            SalvarCaminhosDaTela();
            string rodarNaBandeja = LerValorIni("CONFIG_GERAIS", "RODAR_NA_BANDEJA", this.caminhoIni);

            if (rodarNaBandeja == "Sim")
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                    icBandeja.Visible = true;

                    // Opcional: Mostrar balãozinho
                    icBandeja.ShowBalloonTip(3000, "ExeBoard", "A aplicação continua rodando aqui.", ToolTipIcon.Info);
                }
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            List<Atualizador> atualizadores = new List<Atualizador>();
            string countStr = LerValorIni("ATUALIZADORES", "Count", caminhoIni);
            if (int.TryParse(countStr, out int count))
            {
                for (int i = 0; i <= count; i++)
                {
                    string nomeAtualizadorId = $"NomeAtualizador{i}";
                    string caminhoAtualizadorId = $"CaminhoAtualizador{i}";
                    string nomeAtualizador = LerValorIni("ATUALIZADORES", nomeAtualizadorId, caminhoIni);
                    string caminhoAtualizador = LerValorIni("ATUALIZADORES", caminhoAtualizadorId, caminhoIni);
                    if (!string.IsNullOrWhiteSpace(nomeAtualizador) && !string.IsNullOrWhiteSpace(caminhoAtualizador))
                    {
                        atualizadores.Add(new Atualizador
                        {
                            Nome = nomeAtualizador,
                            Caminho = caminhoAtualizador
                        });

                    }
                }
            }

            if (atualizadores.Any())
            {
                using (frmAtualizador form = new frmAtualizador(atualizadores))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        Atualizador escolhido = form.atualizadorSelecionado;

                        if (escolhido != null && !string.IsNullOrWhiteSpace(escolhido.Caminho))
                        {
                            try
                            {
                                Process.Start(escolhido.Caminho);
                            }
                            catch (Exception ex)
                            {
                                RegistrarLogCopiarDados($"Erro ao abrir o atualizador: {ex.Message}");
                                MessageBox.Show($"Erro ao abrir o programa: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void carregarColaboradores()
        {

            List<Colaborador> contribuidores = new List<Colaborador>
            {
                new Colaborador { Nome = "Fernando Bolson Dias",
                    Funcao = "Arquiteto de Software",
                    GitHub = "Não informado"},

            new Colaborador { Nome = "Giancarlo Abel Giulian",
                Funcao = "Analista de Testes",
                GitHub = "https://github.com/giancarlogiulian"},

            new Colaborador { Nome = "Nicolas Schiochet",
                Funcao = "Analista de Testes",
                GitHub = "https://github.com/nicochet01"}
            };

            dgvColaboradores.DataSource = contribuidores;
            dgvColaboradores.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgvColaboradores.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        }

        private void linkLabel1_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/nicochet01/CopiarExes",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao abrir o link: " + ex.Message);
            }

        }

        private void cbGroupAtualizadores_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {

                ContextMenuStrip cmsAtualizadores = new ContextMenuStrip();
                var marcarTodosItem = cmsAtualizadores.Items.Add("Marcar todos");
                var desmarcarTodosItem = cmsAtualizadores.Items.Add("Desmarcar todos");
                marcarTodosItem.Click += tsmSelecionarTodosAtualizadores_Click;
                desmarcarTodosItem.Click += tsmDesmarcarTodosAtualizadores_Click;

                cmsAtualizadores.Show(cbGroupAtualizadores, e.Location);
            }
        }

        private void tsmSelecionarTodosAtualizadores_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < cbGroupAtualizadores.Items.Count; i++)
            {
                cbGroupAtualizadores.SetItemChecked(i, true);
            }
        }

        private void tsmDesmarcarTodosAtualizadores_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < cbGroupAtualizadores.Items.Count; i++)
            {
                cbGroupAtualizadores.SetItemChecked(i, false);
            }
        }

        private void cbGroupClientes_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {

                ContextMenuStrip cmsClientes = new ContextMenuStrip();
                var marcarTodosItem = cmsClientes.Items.Add("Marcar todos");
                var desmarcarTodosItem = cmsClientes.Items.Add("Desmarcar todos");
                marcarTodosItem.Click += tsmSelecionarTodosClientes_Click;
                desmarcarTodosItem.Click += tsmDesmarcarTodosClientes_Click;

                cmsClientes.Show(cbGroupClientes, e.Location);
            }
        }

        private void tsmSelecionarTodosClientes_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < cbGroupClientes.Items.Count; i++)
            {
                cbGroupClientes.SetItemChecked(i, true);
            }
        }

        private void tsmDesmarcarTodosClientes_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < cbGroupClientes.Items.Count; i++)
            {
                cbGroupClientes.SetItemChecked(i, false);
            }
        }

        private void cbGroupServidores_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {

                ContextMenuStrip cmsServidores = new ContextMenuStrip();
                var marcarTodosItem = cmsServidores.Items.Add("Marcar todos");
                var desmarcarTodosItem = cmsServidores.Items.Add("Desmarcar todos");
                marcarTodosItem.Click += tsmSelecionarTodosServidores_Click;
                desmarcarTodosItem.Click += tsmDesmarcarTodosServidores_Click;

                cmsServidores.Show(cbGroupServidores, e.Location);
            }
        }

        private void tsmSelecionarTodosServidores_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < cbGroupServidores.Items.Count; i++)
            {
                cbGroupServidores.SetItemChecked(i, true);
            }
        }

        private void tsmDesmarcarTodosServidores_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < cbGroupServidores.Items.Count; i++)
            {
                cbGroupServidores.SetItemChecked(i, false);
            }
        }

        private void btnLimparLog_Click(object sender, EventArgs e)
        {
            rtbLog.Clear();
            lock (listaDeLogs)
            {
                listaDeLogs.Clear();
            }
        }

        private void tabCopiarExes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabCopiarExes.SelectedTab == tabServidores)
            {
                CarregarServidoresNoGroupBox();
            }
            else if (tabCopiarExes.SelectedTab == tabConfiguracoes)
            {
                CarregarDadosDeConfiguracao();
            }

        }

        private void CarregarServidoresNoGroupBox()
        {
            groupboxServidores.Controls.Clear();
            int y = 30;

            // Carrega do INI
            string countStr = LerValorIni("APLICACOES_SERVIDORAS", "Count", caminhoIni);
            List<ServidorItem> listaServidores = new List<ServidorItem>();

            if (int.TryParse(countStr, out int count))
            {
                for (int i = 0; i < count; i++)
                {
                    string nome = LerValorIni("APLICACOES_SERVIDORAS", $"Servidor{i}", caminhoIni);
                    string tipo = LerValorIni("APLICACOES_SERVIDORAS", $"Tipo{i}", caminhoIni);
                    string sub = LerValorIni("APLICACOES_SERVIDORAS", $"SubDiretorios{i}", caminhoIni);

                    if (!string.IsNullOrWhiteSpace(nome))
                    {
                        listaServidores.Add(new ServidorItem { Nome = nome, Tipo = tipo, SubDiretorios = sub });
                    }
                }
            }

            listaServidores = listaServidores.OrderBy(x => x.Nome).ToList();

            foreach (var servidor in listaServidores)
            {
                CheckBox chk = new CheckBox();
                chk.Text = servidor.Nome;
                chk.Font = new Font("Segoe UI", 11, FontStyle.Regular);
                chk.Location = new Point(10, y);
                chk.AutoSize = true;

                Label lblStatus = new Label();
                lblStatus.Location = new Point(250, y);
                lblStatus.AutoSize = true;
                // Fonte maior para a bolinha ficar bonita
                lblStatus.Font = new Font("Segoe UI", 16, FontStyle.Bold);

                // --- PREPARAÇÃO DO TOOLTIP E STATUS OCULTO ---
                ToolTip tip = new ToolTip();
                lblStatus.Tag = tip; // Guardamos o tooltip no Tag para usar depois

                // Pega o status inicial
                string status = ObterStatusServidor(servidor);

                // Salva as referências no CheckBox para os botões acharem
                chk.Tag = (servidor, lblStatus);
                chk.CheckedChanged += (s, e) => AtualizarBotoes();

                // Aplica a cor e o AccessibleName
                AtualizarStatus(lblStatus, status);

                groupboxServidores.Controls.Add(chk);
                groupboxServidores.Controls.Add(lblStatus);

                y += 35; // Aumentei um pouco o espaçamento vertical
            }

            AtualizarBotoes();
            timerStatusServidores.Interval = 2000;
            timerStatusServidores.Tick += timerStatusServidores_Tick;
            timerStatusServidores.Start();
        }
        // TAREFA 3 (Ajuste): Status Minimalista (Só a Bolinha)
        private void AtualizarStatus(Label lblStatus, string status)
        {
            string simbolo = "●";
            Color cor = Color.Gray;

            switch (status)
            {
                case "Iniciado":
                    cor = Color.LimeGreen;
                    break;
                case "Parado":
                    cor = Color.Red;
                    break;
                case "Reiniciando": // NOVO CASO
                    cor = Color.Gold; // Amarelo para indicar transição
                    break;
                case "Não Encontrado":
                    cor = Color.Gray;
                    break;
                default:
                    cor = Color.Gold; // Fallback também amarelo
                    break;
            }

            lblStatus.Text = simbolo;
            lblStatus.ForeColor = cor;
            lblStatus.AccessibleName = status;

            if (lblStatus.Tag is ToolTip tip)
            {
                tip.SetToolTip(lblStatus, status);
            }
        }
        private string ObterStatusServidor(ServidorItem servidor)
        {
            try
            {
                if (servidor.Tipo == "Servico")
                {
                    ServiceController sc = new ServiceController(servidor.Nome);
                    switch (sc.Status)
                    {
                        case ServiceControllerStatus.Running:
                            return "Iniciado";
                        case ServiceControllerStatus.Stopped:
                            return "Parado";
                        default:
                            return sc.Status.ToString();
                    }
                }
                else if (servidor.Tipo == "Aplicacao")
                {

                    string nomeProcesso = Path.GetFileNameWithoutExtension(servidor.Nome);

                    Process[] processos = Process.GetProcessesByName(nomeProcesso);

                    if (processos.Length > 0)
                    {
                        return "Iniciado";
                    }
                    else
                    {
                        return "Parado";
                    }
                }
            }
            catch (InvalidOperationException)
            {
                return "Não Encontrado";
            }
            catch (Exception)
            {
                return "Desconhecido";
            }

            return "Desconhecido";
        }

        private void chkSelecionarEmExecucao_CheckedChanged(object sender, EventArgs e)
        {
            if (cbEmExecucao.Checked)
            {
                cbSelecionarParados.Checked = false;
            }

            foreach (Control ctrl in groupboxServidores.Controls)
            {
                if (ctrl is CheckBox chk && chk.Tag is ValueTuple<ServidorItem, Label> dados)
                {
                    Label lblStatus = dados.Item2;

                    if (cbEmExecucao.Checked)
                    {
                        // CORREÇÃO: Usa AccessibleName
                        chk.Checked = string.Equals(lblStatus.AccessibleName, "Iniciado", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        chk.Checked = false;
                    }
                }
            }
            AtualizarBotoes();
        }
        private void cbSelecionarParados_CheckedChanged(object sender, EventArgs e)
        {
            if (cbSelecionarParados.Checked)
            {
                cbEmExecucao.Checked = false;
            }

            foreach (Control ctrl in groupboxServidores.Controls)
            {
                if (ctrl is CheckBox chk && chk.Tag is ValueTuple<ServidorItem, Label> dados)
                {
                    Label lblStatus = dados.Item2;

                    if (cbSelecionarParados.Checked)
                    {
                        // CORREÇÃO: Usa AccessibleName
                        chk.Checked = string.Equals(lblStatus.AccessibleName, "Parado", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        chk.Checked = false;
                    }
                }
            }
            AtualizarBotoes();
        }
        private void AtualizarBotoes()
        {
            bool podeIniciar = false;
            bool podeParar = false;
            bool podeReiniciar = false;

            foreach (var chk in groupboxServidores.Controls.OfType<CheckBox>())
            {
                if (chk.Checked && chk.Tag is ValueTuple<ServidorItem, Label> dados)
                {
                    Label lblStatus = dados.Item2;
                    // CORREÇÃO: Lê AccessibleName em vez de Text
                    string status = lblStatus.AccessibleName;

                    if (status == "Parado")
                    {
                        podeIniciar = true;
                        podeReiniciar = true; // Reiniciar parado = Iniciar
                    }
                    else if (status == "Iniciado")
                    {
                        podeParar = true;
                        podeReiniciar = true;
                    }
                }
            }

            btnIniciar.Enabled = podeIniciar;
            btnParar.Enabled = podeParar;
            btnReiniciar.Enabled = podeReiniciar;
        }
        private void btnIniciar_Click(object sender, EventArgs e)
        {
            foreach (var chk in groupboxServidores.Controls.OfType<CheckBox>())
            {
                if (chk.Checked && chk.Tag is ValueTuple<ServidorItem, Label> dados)
                {
                    ServidorItem servidor = dados.Item1;
                    Label lblStatus = dados.Item2;

                    // CORREÇÃO: Usa AccessibleName
                    if (lblStatus.AccessibleName == "Parado")
                    {
                        IniciarServidor(servidor, false);
                        // Atualiza status forçado para feedback visual imediato
                        AtualizarStatus(lblStatus, "Iniciado");
                    }
                }
            }
            cbSelecionarParados.Checked = false;
            cbEmExecucao.Checked = false;
            AtualizarBotoes();
        }
        private void btnParar_Click(object sender, EventArgs e)
        {
            foreach (var chk in groupboxServidores.Controls.OfType<CheckBox>())
            {
                if (chk.Checked && chk.Tag is ValueTuple<ServidorItem, Label> dados)
                {
                    ServidorItem servidor = dados.Item1;
                    Label lblStatus = dados.Item2;

                    // CORREÇÃO: Usa AccessibleName
                    if (lblStatus.AccessibleName == "Iniciado")
                    {
                        PararServidor(servidor, false);
                        AtualizarStatus(lblStatus, "Parado");
                    }
                }
            }
            cbSelecionarParados.Checked = false;
            cbEmExecucao.Checked = false;
            AtualizarBotoes();
        }
        private void btnReiniciar_Click(object sender, EventArgs e)
        {
            // Bloqueia o botão apenas para evitar cliques duplos, mas mantém a cor original
            btnReiniciar.Enabled = false;

            try
            {
                foreach (var chk in groupboxServidores.Controls.OfType<CheckBox>())
                {
                    if (chk.Checked && chk.Tag is ValueTuple<ServidorItem, Label> dados)
                    {
                        ServidorItem servidor = dados.Item1;
                        Label lblStatus = dados.Item2;
                        string status = lblStatus.AccessibleName;

                        if (status == "Iniciado" || status == "Parado")
                        {
                            // 1. MUDA A BOLINHA PARA AMARELO (REINICIANDO)
                            AtualizarStatus(lblStatus, "Reiniciando");

                            // 2. FORÇA O WINDOWS A PINTAR A BOLINHA AGORA
                            // (Sem isso, ele trava a tela e só pinta no final)
                            Application.DoEvents();

                            // 3. REALIZA O PROCESSO DEMORADO
                            ReiniciarServidor(servidor, false);

                            // 4. VOLTA A BOLINHA PARA VERDE
                            AtualizarStatus(lblStatus, "Iniciado");
                        }
                    }
                }
            }
            finally
            {
                // Libera o botão novamente
                btnReiniciar.Enabled = true;

                // Limpa seleções
                cbSelecionarParados.Checked = false;
                cbEmExecucao.Checked = false;
                AtualizarBotoes();
            }
        }
        private void timerStatusServidores_Tick(object sender, EventArgs e)
        {
            foreach (var chk in groupboxServidores.Controls.OfType<CheckBox>())
            {
                if (chk.Tag is ValueTuple<ServidorItem, Label> dados)
                {
                    ServidorItem servidor = dados.Item1;
                    Label lblStatus = dados.Item2;

                    string statusReal = ObterStatusServidor(servidor);

                    // CORREÇÃO: Comparamos com AccessibleName (que guarda "Iniciado") 
                    // e não com .Text (que guarda "●")
                    if (lblStatus.AccessibleName != statusReal)
                    {
                        AtualizarStatus(lblStatus, statusReal);
                    }
                }
            }
            // Garante que os botões (Iniciar/Parar) fiquem habilitados/desabilitados corretamente
            AtualizarBotoes();
        }
        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void dgvServidores_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void splitContainer1_Panel2_Paint(object sender, PaintEventArgs e)
        {
        }

        private void scConfiguracoes_SplitterMoved(object sender, SplitterEventArgs e)
        {

        }

        private void btnRemoverGlobal_Click(object sender, EventArgs e)
        {
            int totalSelecionado = clbClientes.CheckedItems.Count + clbServidores.CheckedItems.Count + clbAtualizadores.CheckedItems.Count;

            if (totalSelecionado > 0)
            {
                if (MessageBox.Show($"Você tem certeza que deseja remover {totalSelecionado} item(ns) selecionado(s)?", "Confirmar Remoção", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    while (clbClientes.CheckedItems.Count > 0) { clbClientes.Items.Remove(clbClientes.CheckedItems[0]); }
                    while (clbServidores.CheckedItems.Count > 0) { clbServidores.Items.Remove(clbServidores.CheckedItems[0]); }
                    while (clbAtualizadores.CheckedItems.Count > 0) { clbAtualizadores.Items.Remove(clbAtualizadores.CheckedItems[0]); }

                    RegistrarLogCopiarDados($"{totalSelecionado} item(ns) removido(s) da lista de configuração.");
                    configuracoesForamAlteradas = true;
                    AtualizarEstadoBotoesConfig();
                }
            }
        }
        private void btnSalvarConfiguracoes_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Deseja salvar as alterações no arquivo de configuração?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            {
                return;
            }
            try
            {
                RegistrarLogCopiarDados("Salvando alterações no arquivo Inicializar.ini...");

                WritePrivateProfileString("APLICACOES_CLIENTE", null, null, caminhoIni);
                int i = 0;
                foreach (ClienteItem item in clbClientes.Items)
                {
                    WritePrivateProfileString("APLICACOES_CLIENTE", $"Cliente{i}", item.Nome, caminhoIni);
                    WritePrivateProfileString("APLICACOES_CLIENTE", $"Categoria{i}", item.Categoria, caminhoIni);
                    WritePrivateProfileString("APLICACOES_CLIENTE", $"SubDiretorios{i}", item.SubDiretorios ?? "", caminhoIni);
                    i++;
                }
                WritePrivateProfileString("APLICACOES_CLIENTE", "Count", clbClientes.Items.Count.ToString(), caminhoIni);

                WritePrivateProfileString("APLICACOES_SERVIDORAS", null, null, caminhoIni);
                i = 0;
                foreach (ServidorItem item in clbServidores.Items)
                {
                    WritePrivateProfileString("APLICACOES_SERVIDORAS", $"Servidor{i}", item.Nome, caminhoIni);
                    WritePrivateProfileString("APLICACOES_SERVIDORAS", $"Tipo{i}", item.Tipo, caminhoIni);
                    WritePrivateProfileString("APLICACOES_SERVIDORAS", $"Replicar{i}", item.ReplicarParaCopia ? "Sim" : "Nao", caminhoIni);
                    WritePrivateProfileString("APLICACOES_SERVIDORAS", $"SubDiretorios{i}", item.SubDiretorios ?? "", caminhoIni);
                    i++;
                }
                WritePrivateProfileString("APLICACOES_SERVIDORAS", "Count", clbServidores.Items.Count.ToString(), caminhoIni);

                WritePrivateProfileString("BANCO_DE_DADOS", null, null, caminhoIni);
                i = 0;
                foreach (string item in clbAtualizadores.Items)
                {
                    WritePrivateProfileString("BANCO_DE_DADOS", $"Banco{i}", item, caminhoIni);
                    i++;
                }
                WritePrivateProfileString("BANCO_DE_DADOS", "Count", clbAtualizadores.Items.Count.ToString(), caminhoIni);

                configuracoesForamAlteradas = false;
                AtualizarEstadoBotoesConfig();
                RegistrarLogCopiarDados("Alterações salvas com sucesso.");
                MessageBox.Show("Alterações salvas com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);

                RegistrarLogCopiarDados("Atualizando listas da aba principal...");
                CarregarClientes();
                CarregarServidores();
                CarregarBancoDeDados();
                RegistrarLogCopiarDados("Listas da aba principal atualizadas com as novas configurações.");
            }
            catch (Exception ex)
            {
                RegistrarLogCopiarDados($"Erro ao salvar o arquivo de configuração: {ex.Message}");
                MessageBox.Show($"Ocorreu um erro ao salvar o arquivo de configuração:\n\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void btnCancelarAlteracoes_Click(object sender, EventArgs e)
        {
            CarregarDadosDeConfiguracao();
            RegistrarLogCopiarDados("Alterações nas configurações foram canceladas.");
        }
        private void AtualizarEstadoBotoesConfig()
        {
            bool temItensMarcados = clbClientes.CheckedItems.Count > 0 || clbServidores.CheckedItems.Count > 0 || clbAtualizadores.CheckedItems.Count > 0;

            btnRemoverGlobal.Enabled = temItensMarcados;
            btnSalvarConfiguracoes.Enabled = configuracoesForamAlteradas;
            btnCancelarAlteracoes.Enabled = configuracoesForamAlteradas;
        }
        private void btnAdicionarExeServidor_Click(object sender, EventArgs e)
        {
            DialogResult origem = MessageBox.Show(
                "Deseja buscar o arquivo na pasta de ORIGEM (Branch/Rede)?\n\n" +
                "• SIM: Buscar na Origem (Instalação Nova)\n" +
                "• NÃO: Buscar Local (Arquivo já existe no PC)",
                "Selecionar Local", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (origem == DialogResult.Cancel) return;

            string diretorioInicial;
            if (origem == DialogResult.Yes)
            {
                diretorioInicial = edtCaminhoBranch.Text;
                if (string.IsNullOrWhiteSpace(diretorioInicial) || !Directory.Exists(diretorioInicial))
                {
                    MessageBox.Show("A pasta de Origem não está definida.", "Aviso");
                    return;
                }
            }
            else
            {
                string ultimo = LerValorIni("ULTIMOS_CAMINHOS", "UltimoCaminhoAdicionarServidor", caminhoIni);
                diretorioInicial = (!string.IsNullOrEmpty(ultimo) && Directory.Exists(ultimo)) ? ultimo : txtDestinoServidores.Text;
            }

            using (OpenFileDialog dialogo = new OpenFileDialog())
            {
                dialogo.Title = "Selecione a(s) Aplicação(ões) Servidora";
                dialogo.Filter = "Executáveis (*.exe)|*.exe";
                dialogo.Multiselect = true;
                dialogo.InitialDirectory = diretorioInicial;

                if (dialogo.ShowDialog() == DialogResult.OK)
                {
                    if (origem == DialogResult.No)
                    {
                        WritePrivateProfileString("ULTIMOS_CAMINHOS", "UltimoCaminhoAdicionarServidor", Path.GetDirectoryName(dialogo.FileNames[0]), caminhoIni);
                    }

                    // --- CORREÇÃO DA HERANÇA DE PASTA ---
                    string subDiretorioPadrao = "";

                    // MUDANÇA: Procura o primeiro que NÃO SEJA vazio
                    var itemComPasta = clbServidores.Items.OfType<ServidorItem>()
                                        .FirstOrDefault(s => !string.IsNullOrEmpty(s.SubDiretorios));

                    if (itemComPasta != null)
                    {
                        subDiretorioPadrao = itemComPasta.SubDiretorios;
                    }
                    // ------------------------------------

                    int adicionados = 0;
                    foreach (string caminhoCompleto in dialogo.FileNames)
                    {
                        string nomeArquivo = Path.GetFileName(caminhoCompleto);

                        bool jaExiste = clbServidores.Items.OfType<ServidorItem>()
                            .Any(x => x.Nome.Equals(Path.GetFileNameWithoutExtension(nomeArquivo), StringComparison.OrdinalIgnoreCase)
                                   || x.Nome.Equals(nomeArquivo, StringComparison.OrdinalIgnoreCase));

                        if (jaExiste) continue;

                        DialogResult ehServico = MessageBox.Show(
                            $"O arquivo '{nomeArquivo}' roda como um Serviço do Windows?\n\n" +
                            "• SIM = Serviço (ViasoftServerAgroX)\n" +
                            "• NÃO = Aplicação Comum (.exe)",
                            "Tipo de Servidor",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        string tipo = (ehServico == DialogResult.Yes) ? "Servico" : "Aplicacao";
                        string nomeParaSalvar = (ehServico == DialogResult.Yes)
                            ? Path.GetFileNameWithoutExtension(nomeArquivo)
                            : nomeArquivo;

                        clbServidores.Items.Add(new ServidorItem
                        {
                            Nome = nomeParaSalvar,
                            Tipo = tipo,
                            ReplicarParaCopia = true,
                            SubDiretorios = subDiretorioPadrao // Aplica a pasta herdada corretamente
                        });

                        adicionados++;
                    }

                    if (adicionados > 0)
                    {
                        configuracoesForamAlteradas = true;
                        AtualizarEstadoBotoesConfig();
                        RegistrarLogCopiarDados($"{adicionados} servidores adicionados. Lembre-se de salvar!");
                    }
                }
            }
        }
        private void btnAdicionarServico_Click(object sender, EventArgs e)
        {
            string nomeDoServico = "";
            using (frmAdicionarServico formAdicionar = new frmAdicionarServico())
            {
                if (formAdicionar.ShowDialog(this) == DialogResult.OK)
                {
                    nomeDoServico = formAdicionar.NomeDoServico;
                }
            }

            if (!string.IsNullOrWhiteSpace(nomeDoServico))
            {
                bool jaExiste = clbServidores.Items.OfType<ServidorItem>().Any(item => string.Equals(item.Nome, nomeDoServico, StringComparison.OrdinalIgnoreCase));
                if (jaExiste)
                {
                    MessageBox.Show($"O item '{nomeDoServico}' já existe na lista de servidores.", "Item Duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                bool replicar = false;

                clbServidores.Items.Add(new ServidorItem { Nome = nomeDoServico, Tipo = "Servico", ReplicarParaCopia = replicar });
                RegistrarLogCopiarDados($"Serviço '{nomeDoServico}' adicionado à lista de configuração.");
                configuracoesForamAlteradas = true;
                AtualizarEstadoBotoesConfig();
            }
        }
        private void btnAdicionarCliente_Click(object sender, EventArgs e)
        {
            // ... (Bloco de perguntas de ORIGEM/BRANCH continua igual, mantenha o MessageBox Sim/Não) ...
            // Vou resumir para focar na lógica nova:

            string diretorioInicial = "";
            DialogResult origem = MessageBox.Show(
                "Deseja buscar o arquivo na pasta de ORIGEM (Branch/Rede)?\n\n" +
                "• SIM: Buscar na Origem (Instalação Nova)\n" +
                "• NÃO: Buscar Local (Arquivo já existe no PC)",
                "Selecionar Local", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (origem == DialogResult.Cancel) return;

            if (origem == DialogResult.Yes)
            {
                diretorioInicial = edtCaminhoBranch.Text;
                if (string.IsNullOrWhiteSpace(diretorioInicial) || !Directory.Exists(diretorioInicial)) return;
            }
            else
            {
                string ultimo = LerValorIni("ULTIMOS_CAMINHOS", "UltimoCaminhoAdicionarExe", caminhoIni);
                diretorioInicial = (!string.IsNullOrEmpty(ultimo) && Directory.Exists(ultimo)) ? ultimo : txtDestinoClientes.Text;
            }

            using (OpenFileDialog dialogo = new OpenFileDialog())
            {
                dialogo.Title = "Selecione a(s) Aplicação(ões) Cliente";
                dialogo.Filter = "Aplicações (*.exe)|*.exe";
                dialogo.Multiselect = true; // Mantém seleção múltipla
                dialogo.InitialDirectory = diretorioInicial;

                if (dialogo.ShowDialog() == DialogResult.OK)
                {
                    if (origem == DialogResult.No)
                    {
                        string novoDiretorio = Path.GetDirectoryName(dialogo.FileNames[0]);
                        WritePrivateProfileString("ULTIMOS_CAMINHOS", "UltimoCaminhoAdicionarExe", novoDiretorio, caminhoIni);
                    }

                    // --- A MÁGICA ACONTECE AQUI ---
                    // O sistema varre a lista e o disco para decidir qual é a pasta padrão (ex: "Agro")
                    string subDiretorioCalculado = DescobrirSubdiretorioPredominante(clbClientes.Items, txtDestinoClientes.Text);
                    // -----------------------------

                    int adicionados = 0;
                    foreach (string caminhoCompleto in dialogo.FileNames)
                    {
                        string nomeDoArquivo = Path.GetFileName(caminhoCompleto);
                        bool jaExiste = clbClientes.Items.OfType<ClienteItem>().Any(item => string.Equals(item.Nome, nomeDoArquivo, StringComparison.OrdinalIgnoreCase));

                        if (jaExiste) continue;

                        var novoCliente = new ClienteItem
                        {
                            Nome = nomeDoArquivo,
                            Categoria = "Padrão",
                            SubDiretorios = subDiretorioCalculado // <--- Aplica automaticamente "Agro"
                        };

                        clbClientes.Items.Add(novoCliente);
                        adicionados++;

                        if (!string.IsNullOrEmpty(subDiretorioCalculado))
                            RegistrarLogCopiarDados($"Item {nomeDoArquivo} assumiu a pasta: '\\{subDiretorioCalculado}'");
                    }

                    if (adicionados > 0)
                    {
                        configuracoesForamAlteradas = true;
                        AtualizarEstadoBotoesConfig();
                    }
                }
            }
        }
        private void btnRemoverCliente_Click_1(object sender, EventArgs e)
        {
            List<DataGridViewRow> linhasParaRemover = new List<DataGridViewRow>();


            if (linhasParaRemover.Count > 0)
            {
                if (MessageBox.Show($"Você tem certeza que deseja remover {linhasParaRemover.Count} cliente(s)?",
                                      "Confirmar Remoção",
                                      MessageBoxButtons.YesNo,
                                      MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    configuracoesForamAlteradas = true;
                    RegistrarLogCopiarDados($"{linhasParaRemover.Count} cliente(s) removidos da lista de configuração.");
                }
            }
            else
            {
                MessageBox.Show("Nenhum item foi selecionado para remoção.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void button2_Click_3(object sender, EventArgs e)
        {
            CarregarDadosDeConfiguracao();
            RegistrarLogCopiarDados("Alterações nas configurações foram canceladas.");

        }


        private void tabCopiarExes_Deselecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == tabConfiguracoes && configuracoesForamAlteradas)
            {
                DialogResult resultado = MessageBox.Show(
                    "Você possui alterações não salvas. Deseja salvá-las antes de sair?",
                    "Alterações Pendentes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question
                );

                switch (resultado)
                {
                    case DialogResult.Yes:
                        btnSalvarConfiguracoes_Click(null, null);
                        break;

                    case DialogResult.No:
                        CarregarDadosDeConfiguracao();
                        break;

                    case DialogResult.Cancel:
                        e.Cancel = true;
                        break;
                }
            }
        }
        private void clb_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            this.BeginInvoke((System.Windows.Forms.MethodInvoker)delegate { AtualizarEstadoBotoesConfig(); });
        }

        private void tsmMarcarDesmarcarTodos_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            if (menuItem == null) return;

            ContextMenuStrip contextMenu = menuItem.Owner as ContextMenuStrip;
            if (contextMenu == null) return;

            CheckedListBox listBox = contextMenu.SourceControl as CheckedListBox;

            if (listBox != null)
            {
                bool deveMarcar = (listBox.CheckedItems.Count == 0);

                for (int i = 0; i < listBox.Items.Count; i++)
                {
                    listBox.SetItemChecked(i, deveMarcar);
                }
            }
        }

        private void Placeholder_Enter(object sender, EventArgs e)
        {
            TextBox txt = sender as TextBox;
            string placeholder = "Informe o caminho da pasta aqui";

            if (txt != null && txt.Text == placeholder && txt.ForeColor == SystemColors.GrayText)
            {
                txt.Text = "";
                txt.ForeColor = SystemColors.WindowText;
            }
        }

        private void Placeholder_Leave(object sender, EventArgs e)
        {
            TextBox txt = sender as TextBox;
            string placeholder = "Informe o caminho da pasta aqui";

            if (txt != null && string.IsNullOrWhiteSpace(txt.Text))
            {
                txt.Text = placeholder;
                txt.ForeColor = SystemColors.GrayText;
            }
        }

        private void btnProcurarAtualizadores_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialogo = new FolderBrowserDialog())
            {
                dialogo.Description = "Selecione o diretório de destino para Atualizadores";
                dialogo.ShowNewFolderButton = true;

                string caminhoInicial = txtDestinoAtualizadores.Text;
                string placeholder = "Informe o caminho da pasta aqui";

                if (string.IsNullOrWhiteSpace(caminhoInicial) || caminhoInicial == placeholder)
                {
                    caminhoInicial = LerValorIni("ULTIMOS_CAMINHOS", "UltimoCaminhoAtualizadores", caminhoIni);
                }

                while (!string.IsNullOrEmpty(caminhoInicial) && !Directory.Exists(caminhoInicial))
                {
                    caminhoInicial = Path.GetDirectoryName(caminhoInicial);
                }

                if (!string.IsNullOrEmpty(caminhoInicial))
                {
                    dialogo.InitialDirectory = caminhoInicial;
                }

                if (dialogo.ShowDialog() == DialogResult.OK)
                {
                    txtDestinoAtualizadores.Text = dialogo.SelectedPath;
                    txtDestinoAtualizadores.ForeColor = SystemColors.WindowText;
                    WritePrivateProfileString("ULTIMOS_CAMINHOS", "UltimoCaminhoAtualizadores", dialogo.SelectedPath, caminhoIni);
                    WritePrivateProfileString("CAMINHOS", "PASTA_DADOS", dialogo.SelectedPath, caminhoIni);
                }
            }
        }
        private void btnProcurarClientes_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialogo = new FolderBrowserDialog())
            {
                dialogo.Description = "Selecione o diretório de destino para Clientes";
                dialogo.ShowNewFolderButton = true;

                string caminhoInicial = txtDestinoClientes.Text;
                string placeholder = "Informe o caminho da pasta aqui";

                // Se for o placeholder ou vazio, tenta pegar do INI
                if (string.IsNullOrWhiteSpace(caminhoInicial) || caminhoInicial == placeholder)
                {
                    caminhoInicial = LerValorIni("ULTIMOS_CAMINHOS", "UltimoCaminhoClientes", caminhoIni);
                }

                // Garante que o caminho (ou um pai dele) exista
                while (!string.IsNullOrEmpty(caminhoInicial) && !Directory.Exists(caminhoInicial))
                {
                    caminhoInicial = Path.GetDirectoryName(caminhoInicial);
                }

                // Define o diretório inicial para ENTRAR na pasta
                if (!string.IsNullOrEmpty(caminhoInicial))
                {
                    dialogo.InitialDirectory = caminhoInicial;
                }

                if (dialogo.ShowDialog() == DialogResult.OK)
                {
                    txtDestinoClientes.Text = dialogo.SelectedPath;
                    txtDestinoClientes.ForeColor = SystemColors.WindowText;
                    WritePrivateProfileString("ULTIMOS_CAMINHOS", "UltimoCaminhoClientes", dialogo.SelectedPath, caminhoIni);
                    WritePrivateProfileString("CAMINHOS", "PASTA_CLIENT", dialogo.SelectedPath, caminhoIni);
                }
            }
        }
        private void btnProcurarServidores_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialogo = new FolderBrowserDialog())
            {
                dialogo.Description = "Selecione o diretório de destino para Servidores";
                dialogo.ShowNewFolderButton = true;

                string caminhoInicial = txtDestinoServidores.Text;
                string placeholder = "Informe o caminho da pasta aqui";

                if (string.IsNullOrWhiteSpace(caminhoInicial) || caminhoInicial == placeholder)
                {
                    caminhoInicial = LerValorIni("ULTIMOS_CAMINHOS", "UltimoCaminhoServidores", caminhoIni);
                }

                while (!string.IsNullOrEmpty(caminhoInicial) && !Directory.Exists(caminhoInicial))
                {
                    caminhoInicial = Path.GetDirectoryName(caminhoInicial);
                }

                if (!string.IsNullOrEmpty(caminhoInicial))
                {
                    dialogo.InitialDirectory = caminhoInicial;
                }

                if (dialogo.ShowDialog() == DialogResult.OK)
                {
                    txtDestinoServidores.Text = dialogo.SelectedPath;
                    txtDestinoServidores.ForeColor = SystemColors.WindowText;
                    WritePrivateProfileString("ULTIMOS_CAMINHOS", "UltimoCaminhoServidores", dialogo.SelectedPath, caminhoIni);
                    WritePrivateProfileString("CAMINHOS", "PASTA_SERVER", dialogo.SelectedPath, caminhoIni);
                }
            }
        }
        private void btnAdicionarAtualizador_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialogo = new FolderBrowserDialog())
            {
                dialogo.Description = "Selecione a pasta do Atualizador (ex: Firebird, Oracle)";
                dialogo.ShowNewFolderButton = false;

                string ultimoCaminho = LerValorIni("ULTIMOS_CAMINHOS", "UltimoCaminhoPastaAtualizador", caminhoIni);

                if (!string.IsNullOrEmpty(ultimoCaminho) && Directory.Exists(ultimoCaminho))
                {
                    dialogo.InitialDirectory = ultimoCaminho;
                }
                else
                {
                    string caminhoBranch = edtCaminhoBranch.Text;
                    string dePastaDados = LerValorIni("CAMINHOS", "DE_PASTA_DADOS", caminhoIni);
                    string caminhoPadrao = Path.Combine(caminhoBranch, dePastaDados);

                    if (Directory.Exists(caminhoPadrao))
                    {
                        dialogo.InitialDirectory = caminhoPadrao;
                    }
                    else if (Directory.Exists(caminhoBranch))
                    {
                        dialogo.InitialDirectory = caminhoBranch;
                    }
                }

                if (dialogo.ShowDialog() == DialogResult.OK)
                {
                    WritePrivateProfileString("ULTIMOS_CAMINHOS", "UltimoCaminhoPastaAtualizador", dialogo.SelectedPath, caminhoIni);

                    string nomePasta = new DirectoryInfo(dialogo.SelectedPath).Name;

                    bool jaExiste = clbAtualizadores.Items.OfType<string>().Any(item => string.Equals(item, nomePasta, StringComparison.OrdinalIgnoreCase));

                    if (jaExiste)
                    {
                        RegistrarLogCopiarDados($"Atualizador '{nomePasta}' já existe na lista. Ignorando.");
                        MessageBox.Show($"A pasta '{nomePasta}' já está na lista.", "Item Duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        clbAtualizadores.Items.Add(nomePasta);
                        RegistrarLogCopiarDados($"Pasta de atualizador '{nomePasta}' adicionada à lista de configuração.");
                        configuracoesForamAlteradas = true;
                        AtualizarEstadoBotoesConfig();
                    }
                }
            }
        }
        private void CopiarDiretorioComLog(string sourceDir, string destDir, string nomePasta)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(sourceDir);
                if (!dir.Exists)
                {
                    throw new DirectoryNotFoundException($"Diretório de origem não encontrado: {sourceDir}");
                }

                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    RegistrarLogCopiarDados($"Criado diretório: {destDir}", Color.Gray);
                }

                foreach (FileInfo file in dir.GetFiles())
                {
                    string tempPath = Path.Combine(destDir, file.Name);
                    file.CopyTo(tempPath, true);
                }

                foreach (DirectoryInfo subdir in dir.GetDirectories())
                {
                    string tempPath = Path.Combine(destDir, subdir.Name);
                    CopiarDiretorioComLog(subdir.FullName, tempPath, subdir.Name);
                }

                RegistrarLogCopiarDados($"OK: Pasta {nomePasta} copiada para {destDir}", Color.DarkGreen);
            }
            catch (Exception ex)
            {
                RegistrarLogCopiarDados($"ERRO ao copiar a pasta {nomePasta}: {ex.Message}", Color.Red);
            }
        }

        private void btnFiltrarErros_Click(object sender, EventArgs e)
        {
            RepopularLogs(Color.Red);
        }

        private void btnFiltrarSucesso_Click(object sender, EventArgs e)
        {
            RepopularLogs(Color.DarkGreen);
        }

        private void btnMostrarTodos_Click(object sender, EventArgs e)
        {
            RepopularLogs(null);
        }

        private void SalvarCaminhosDaTela()
        {
            string placeholder = "Informe o caminho da pasta aqui";

            // 1. Salvar caminho da Branch (Origem)
            if (!string.IsNullOrWhiteSpace(edtCaminhoBranch.Text))
            {
                WritePrivateProfileString("CAMINHOS", "DE", edtCaminhoBranch.Text, caminhoIni);
            }

            // 2. Salvar caminho dos Atualizadores (Destino)
            if (!string.IsNullOrWhiteSpace(txtDestinoAtualizadores.Text) && txtDestinoAtualizadores.Text != placeholder)
            {
                WritePrivateProfileString("CAMINHOS", "PASTA_DADOS", txtDestinoAtualizadores.Text, caminhoIni);
                WritePrivateProfileString("ULTIMOS_CAMINHOS", "UltimoCaminhoAtualizadores", txtDestinoAtualizadores.Text, caminhoIni);
            }

            // 3. Salvar caminho dos Clientes (Destino)
            if (!string.IsNullOrWhiteSpace(txtDestinoClientes.Text) && txtDestinoClientes.Text != placeholder)
            {
                WritePrivateProfileString("CAMINHOS", "PASTA_CLIENT", txtDestinoClientes.Text, caminhoIni);
                WritePrivateProfileString("ULTIMOS_CAMINHOS", "UltimoCaminhoClientes", txtDestinoClientes.Text, caminhoIni);
            }

            // 4. Salvar caminho dos Servidores (Destino)
            if (!string.IsNullOrWhiteSpace(txtDestinoServidores.Text) && txtDestinoServidores.Text != placeholder)
            {
                WritePrivateProfileString("CAMINHOS", "PASTA_SERVER", txtDestinoServidores.Text, caminhoIni);
                WritePrivateProfileString("ULTIMOS_CAMINHOS", "UltimoCaminhoServidores", txtDestinoServidores.Text, caminhoIni);
            }
        }

        // --- NOVO MÉTODO: Gerencia o padrão sem criar pastas invasivas ---
        private void ConfigurarCaminhosPadraoSeVazio(TextBox txt, string caminhoPadrao, string chaveIni, string chaveUltimo)
        {
            string valorIni = LerValorIni("CAMINHOS", chaveIni, caminhoIni);
            string placeholder = "Informe o caminho da pasta aqui";

            // Se no INI estiver vazio ou for o placeholder, sugere o padrão (Ex: C:\Viasoft\Dados)
            // Se o usuário já salvou algo antes, respeita o que ele salvou.
            if (string.IsNullOrWhiteSpace(valorIni) || valorIni == placeholder)
            {
                txt.Text = caminhoPadrao;
            }
            else
            {
                txt.Text = valorIni;
            }

            txt.ForeColor = SystemColors.WindowText;
        }

        // --- NOVO MÉTODO: Descobre qual é a pasta predominante (Efeito Manada Inteligente) ---
        private string DescobrirSubdiretorioPredominante(System.Windows.Forms.ListBox.ObjectCollection itens, string raizDestino)
        {
            // Dicionário para contar: "NomeDaPasta" -> Quantidade de vezes que aparece
            Dictionary<string, int> contagemPastas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in itens)
            {
                string subDir = "";
                string nomeArquivo = "";

                // Identifica se é Cliente ou Servidor para pegar as propriedades
                if (item is ClienteItem c) { subDir = c.SubDiretorios; nomeArquivo = c.Nome; }
                else if (item is ServidorItem s) { subDir = s.SubDiretorios; nomeArquivo = s.Nome; }

                // 1. Se já está configurado no INI, usa essa informação
                if (!string.IsNullOrEmpty(subDir))
                {
                    if (!contagemPastas.ContainsKey(subDir)) contagemPastas[subDir] = 0;
                    contagemPastas[subDir]++;
                }
                else
                {
                    // 2. Se NÃO está no INI (está vazio), tenta achar no DISCO FÍSICO agora
                    if (Directory.Exists(raizDestino))
                    {
                        try
                        {
                            // Procura o arquivo em todas as subpastas
                            string[] encontrados = Directory.GetFiles(raizDestino, nomeArquivo, SearchOption.AllDirectories);

                            if (encontrados.Length > 0)
                            {
                                // Achou! Vamos calcular a pasta relativa
                                string pastaCompleta = Path.GetDirectoryName(encontrados[0]);

                                // Se "C:\Viasoft\Client\Agro" começa com "C:\Viasoft\Client"
                                if (pastaCompleta.StartsWith(raizDestino, StringComparison.OrdinalIgnoreCase) && pastaCompleta.Length > raizDestino.Length)
                                {
                                    string resto = pastaCompleta.Substring(raizDestino.Length);
                                    // Limpa as barras para sobrar só "Agro"
                                    string pastaDetectada = resto.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                                    if (!string.IsNullOrEmpty(pastaDetectada))
                                    {
                                        if (!contagemPastas.ContainsKey(pastaDetectada)) contagemPastas[pastaDetectada] = 0;
                                        contagemPastas[pastaDetectada]++;
                                    }
                                }
                            }
                        }
                        catch { /* Ignora erros de permissão */ }
                    }
                }
            }

            // Retorna a pasta que apareceu mais vezes (Moda Estatística)
            // Se não achou nada, retorna vazio (Raiz)
            return contagemPastas.OrderByDescending(x => x.Value).Select(x => x.Key).FirstOrDefault() ?? "";
        }
    }
}