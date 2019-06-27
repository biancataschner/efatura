using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacWin.EFaturas
{
    class Fatura
    {
        public string IdDocumento { get; set; }
        public string Comerciante { get; set; }
        public string NifEmitente { get; set; }
        public string Situacao { get; set; }
        public string Numero { get; set; }
        public string Codigo { get; set; }
        public string DataEmissao { get; set; }
        public string Iva { get; set; }
        public string ValorTotal { get; set; }
        public List<Iva> ListaIva { get; set; }
    }
}
