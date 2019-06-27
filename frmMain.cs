using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace MacWin.EFaturas
{
    public partial class frmMain : Form
    {        
        private List<Fatura> Faturas { get; set; }

        public frmMain()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            CookieContainer cookieJar = new CookieContainer();
            HttpClientHandler handler = new HttpClientHandler()
            {
                CookieContainer = cookieJar
            };
            handler.UseCookies = true;
            handler.UseDefaultCredentials = false;

            HttpClient hc = new HttpClient(handler);

            var loginRequest = hc.GetStringAsync("https://www.acesso.gov.pt/jsp/loginRedirectForm.jsp?path=painelAdquirente.action&partID=EFPF");
            var htmlLogin = new HtmlAgilityPack.HtmlDocument();
            htmlLogin.LoadHtml(loginRequest.Result);

            var _csrf = htmlLogin.DocumentNode.SelectSingleNode("//input[@name='_csrf']/@value").GetAttributeValue("value", "");

            var resultLogin = hc.PostAsync("https://www.acesso.gov.pt/jsp/submissaoFormularioLogin?path=painelAdquirente.action&partID=EFPF&authVersion=1&selectedAuthMethod=N", new StringContent("username=" + txtUtilizador.Text + "&password=" + txtPalavraPasse.Text + "&_csrf=" + _csrf, Encoding.UTF8, "application/x-www-form-urlencoded"));
            var contents = resultLogin.Result.Content.ReadAsStringAsync();
            var htmlLogin2 = new HtmlAgilityPack.HtmlDocument();
            htmlLogin2.LoadHtml(contents.Result);
            var sign = htmlLogin2.DocumentNode.SelectSingleNode("//input[@name='sign']/@value").GetAttributeValue("value", "");
            var userID = htmlLogin2.DocumentNode.SelectSingleNode("//input[@name='userID']/@value").GetAttributeValue("value", "");
            var sessionID = htmlLogin2.DocumentNode.SelectSingleNode("//input[@name='sessionID']/@value").GetAttributeValue("value", "");
            var nif = htmlLogin2.DocumentNode.SelectSingleNode("//input[@name='nif']/@value").GetAttributeValue("value", "");
            var tc = htmlLogin2.DocumentNode.SelectSingleNode("//input[@name='tc']/@value").GetAttributeValue("value", "");
            var tv = htmlLogin2.DocumentNode.SelectSingleNode("//input[@name='tv']/@value").GetAttributeValue("value", "");
            var userName = htmlLogin2.DocumentNode.SelectSingleNode("//input[@name='userName']/@value").GetAttributeValue("value", "");
            var partID = htmlLogin2.DocumentNode.SelectSingleNode("//input[@name='partID']/@value").GetAttributeValue("value", "");

            var formVariables = new List<KeyValuePair<string, string>>();
            formVariables.Add(new KeyValuePair<string, string>("sign", sign));
            formVariables.Add(new KeyValuePair<string, string>("userID", userID));
            formVariables.Add(new KeyValuePair<string, string>("sessionID", sessionID));
            formVariables.Add(new KeyValuePair<string, string>("nif", nif));
            formVariables.Add(new KeyValuePair<string, string>("tc", tc));
            formVariables.Add(new KeyValuePair<string, string>("tv", tv));
            formVariables.Add(new KeyValuePair<string, string>("userName", userName));
            formVariables.Add(new KeyValuePair<string, string>("partID", partID));
            var formContent = new FormUrlEncodedContent(formVariables);

            //TODO: await? e ai nao usa o result, retorno do metodo tb precisa ser async Task
            var painelAdquirentePost = hc.PostAsync("https://faturas.portaldasfinancas.gov.pt/painelAdquirente.action", formContent).Result;

            var url = "";
            Task<string> json = null;
            dynamic stuff;

            //primeiro tenta pegar do ano todo
            url = string.Format("https://faturas.portaldasfinancas.gov.pt/json/obterDocumentosAdquirente.action?dataInicioFilter={0}&dataFimFilter={1}&ambitoAquisicaoFilter=TODOS", dateTimePicker1.Value.ToString("yyyy-MM-dd"), dateTimePicker2.Value.ToString("yyyy-MM-dd"));
            json = hc.GetStringAsync(url);
            stuff = JObject.Parse(json.Result); //stuff.totalElementos
            var linhas = (Newtonsoft.Json.Linq.JArray)stuff.linhas;
            this.Faturas = new List<Fatura>();

            //TODO: while == 300 vai quebrando... e armazena o primeiro perido, algo assim OU if dentro de if**
            if (stuff.numElementos == 300)
            {
                IEnumerable<DateTime> dates = Enumerable.Range(0, dateTimePicker2.Value.Subtract(dateTimePicker1.Value).Days + 1).Select(d => dateTimePicker1.Value.AddDays(d));

                Parallel.ForEach(dates, date =>
                {
                    url = string.Format("https://faturas.portaldasfinancas.gov.pt/json/obterDocumentosAdquirente.action?dataInicioFilter={0}&dataFimFilter={0}&ambitoAquisicaoFilter=TODOS", date.ToString("yyyy-MM-dd"));
                    json = hc.GetStringAsync(url);
                    stuff = JObject.Parse(json.Result);

                    Parallel.ForEach(linhas, linha =>
                    {

                        var fatura = new Fatura
                        {
                            IdDocumento = ((dynamic)linha).idDocumento,
                            NifEmitente = ((dynamic)linha).nifEmitente,
                            Comerciante = ((dynamic)linha).nomeEmitente,
                            DataEmissao = ((dynamic)linha).dataEmissaoDocumento,
                            Numero = ((dynamic)linha).numerodocumento,
                            ValorTotal = ((dynamic)linha).valorTotal,
                            Iva = ((dynamic)linha).valorTotalIva,
                            ListaIva = new List<Iva>()
                        };
                        Faturas.Add(fatura);
                    });
                });
            }
            else
            {
                Parallel.ForEach(linhas, linha =>
                {

                    var fatura = new Fatura
                    {
                        IdDocumento = ((dynamic)linha).idDocumento,
                        NifEmitente = ((dynamic)linha).nifEmitente,
                        Comerciante = ((dynamic)linha).nomeEmitente,
                        DataEmissao = ((dynamic)linha).dataEmissaoDocumento,
                        Numero = ((dynamic)linha).numerodocumento,
                        ValorTotal = ((dynamic)linha).valorTotal,
                        Iva = ((dynamic)linha).valorTotalIva,
                        ListaIva = new List<Iva>()
                    };
                    Faturas.Add(fatura);
                });
            }

            //carrega fatura por fatura para obter detalhe do iva
            Parallel.ForEach(Faturas, fat =>
            {
                url = string.Format("https://faturas.portaldasfinancas.gov.pt/detalheDocumentoAdquirente.action?idDocumento={0}&dataEmissaoDocumento={1}", fat.IdDocumento, fat.DataEmissao);
                var html = hc.GetStringAsync(url);

                var htmlDocument = new HtmlAgilityPack.HtmlDocument();
                htmlDocument.LoadHtml(html.Result);
                
                var script = htmlDocument.DocumentNode.Descendants()
                                             .Where(n => n.Name == "script" && n.InnerText.Contains("dadosLinhasDocumento"))
                                             .First().InnerText;

                string pattern = @"dadosLinhasDocumento = \{?(.*)}?\;";
                Match match = Regex.Matches(script, pattern)[0];
                stuff = JArray.Parse(match.Groups[1].Value);

           
                Parallel.ForEach((Newtonsoft.Json.Linq.JArray)stuff, td_iva =>
                {
                    var iva = new Iva()
                    {
                        BaseTributavel = ((dynamic)td_iva).valorBaseTributavel, //TODO: dividir por mil
                        Taxa = ((dynamic)td_iva).taxaIva,
                        Total = ((dynamic)td_iva).valorTotal,
                        ValorIva = ((dynamic)td_iva).valorIva
                    };

                    fat.ListaIva.Add(iva);
                });
            });

            dataGridView1.DataSource = Faturas;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            txtUtilizador.Text = "xx";
            txtPalavraPasse.Text = "xx";
        }

        private void tabPage3_Click(object sender, EventArgs e)
        {

        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            var idDocumento =  dataGridView1[e.ColumnIndex, e.RowIndex].Value.ToString();
            dataGridView2.DataSource = Faturas.FirstOrDefault(f => f.IdDocumento == idDocumento).ListaIva;
            tabControl1.SelectTab(1);
        }
    }
}
