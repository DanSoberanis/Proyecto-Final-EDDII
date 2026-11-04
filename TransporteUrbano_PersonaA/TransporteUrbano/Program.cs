using System;
using System.Windows.Forms;
using TransporteUrbano.Estructuras;
using TransporteUrbano.Servicios;
using TransporteUrbano.UI;

namespace TransporteUrbano
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // El grafo lo entrega la fábrica: si la Persona B todavía no lo implementó,
            // se usa un sustituto y el programa abre igual (ver Estructuras/FabricaGrafo.cs).
            // Opción de respaldo: TransporteUrbano.exe --ligero abre con el mapa que no necesita internet.
            foreach (string a in args)
                if (a == "--ligero") FabricaMapa.UsarMapaReal = false;

            // Errores inesperados (por ejemplo, de la descarga de mapas) se muestran en vez de cerrar el programa.
            Application.ThreadException += (s, e) =>
                MessageBox.Show(e.Exception.Message, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            var sistema = new SistemaTransporte(new GrafoTransporte());
            Application.Run(new FormPrincipal(sistema));
        }
    }
}
