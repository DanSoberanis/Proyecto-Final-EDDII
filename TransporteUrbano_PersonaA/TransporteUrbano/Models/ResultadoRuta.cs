using TransporteUrbano.Estructuras;

namespace TransporteUrbano.Models
{
    public enum CriterioPeso { Tiempo, Costo }

    /// <summary>
    /// Resultado de calcular una ruta. La Persona B (Dijkstra) solo llena Exito, Mensaje,
    /// Camino y PesoTotal; los totales de tiempo y costo y los Pasos los completa SistemaTransporte.
    /// </summary>
    public class ResultadoRuta
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = "";
        public ListaEnlazada<string> Camino { get; set; } = new ListaEnlazada<string>();
        public double PesoTotal { get; set; }
        public double TiempoTotal { get; set; }
        public double CostoTotal { get; set; }
        public CriterioPeso Criterio { get; set; }
        public ListaEnlazada<string> Pasos { get; set; } = new ListaEnlazada<string>();

        public static ResultadoRuta Fallo(string mensaje)
        {
            return new ResultadoRuta { Exito = false, Mensaje = mensaje };
        }
    }
}
