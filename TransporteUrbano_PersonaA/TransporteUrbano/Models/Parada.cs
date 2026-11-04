namespace TransporteUrbano.Models
{
    /// <summary>Una parada de bus. El código es único y es la clave de la Tabla Hash.</summary>
    public class Parada
    {
        public string Codigo { get; }
        public string Nombre { get; }
        public double Latitud { get; }
        public double Longitud { get; }

        public Parada(string codigo, string nombre, double latitud, double longitud)
        {
            Codigo = codigo;
            Nombre = nombre;
            Latitud = latitud;
            Longitud = longitud;
        }

        public override string ToString() { return Nombre + " (" + Codigo + ")"; }
    }
}
