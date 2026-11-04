namespace TransporteUrbano.Models
{
    /// <summary>Tramo entre dos paradas. Si Unidireccional es true solo se puede ir de Origen a Destino.</summary>
    public class Ruta
    {
        public string Origen { get; }
        public string Destino { get; }
        public double Tiempo { get; }   // minutos
        public double Costo { get; }    // quetzales
        public bool Unidireccional { get; }

        public Ruta(string origen, string destino, double tiempo, double costo, bool unidireccional)
        {
            Origen = origen;
            Destino = destino;
            Tiempo = tiempo;
            Costo = costo;
            Unidireccional = unidireccional;
        }
    }
}
