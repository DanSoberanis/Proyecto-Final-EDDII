using System;
using System.Windows.Forms;
using TransporteUrbano.Estructuras;
using TransporteUrbano.Servicios;

namespace TransporteUrbano.UI
{
    /// <summary>
    /// Lo que FormPrincipal necesita de un mapa. Lo implementan MapaControl (dibujado con GDI+, sin internet)
    /// y MapaGMapControl (mapa real de OpenStreetMap con GMap.NET). Así se puede cambiar de uno a otro
    /// sin tocar el resto de la interfaz.
    /// </summary>
    public interface IMapaRed
    {
        Control Vista { get; }
        SistemaTransporte Sistema { get; set; }
        bool ModoElegirUbicacion { get; }
        event Action<string> ParadaClick;
        event Action<double, double> UbicacionElegida;

        void Refrescar();
        void SeleccionarParada(string codigo, bool centrar);
        void MostrarCamino(ListaEnlazada<string> codigos);
        void MostrarSecuencia(ListaEnlazada<string> codigos);
        void LimpiarResaltado();
        void IniciarElegirUbicacion();
    }
}
