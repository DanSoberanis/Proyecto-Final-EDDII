using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using TransporteUrbano.Servicios;

namespace TransporteUrbano.UI
{
    /// <summary>
    /// Decide qué mapa se muestra. Si el mapa real (GMap.NET) no se puede iniciar por cualquier motivo,
    /// se usa el mapa ligero, que siempre funciona. También se puede arrancar con: TransporteUrbano.exe --ligero
    /// </summary>
    public static class FabricaMapa
    {
        public static bool UsarMapaReal { get; set; } = true;

        public static IMapaRed Crear(SistemaTransporte sistema, out string aviso)
        {
            aviso = null;
            if (UsarMapaReal)
            {
                try
                {
                    return CrearReal(sistema);
                }
                catch (Exception ex)
                {
                    UsarMapaReal = false;
                    aviso = "No se pudo iniciar el mapa real (" + ex.GetType().Name + "). Se usa el mapa ligero.";
                }
            }
            var ligero = new MapaControl { Dock = DockStyle.Fill };
            ligero.Sistema = sistema;
            return ligero;
        }

        // Método aparte y sin "inlining": si la librería GMap no se encuentra, el error ocurre aquí dentro del try.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IMapaRed CrearReal(SistemaTransporte sistema)
        {
            var real = new MapaGMapControl { Dock = DockStyle.Fill };
            real.Sistema = sistema;
            return real;
        }
    }
}
