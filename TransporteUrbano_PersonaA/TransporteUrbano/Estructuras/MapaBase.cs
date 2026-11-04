namespace TransporteUrbano.Estructuras
{
    /// <summary>
    /// [PERSONA A] Capa base del mapa, incrustada en el código para que funcione sin internet:
    /// contorno de Guatemala (simplificado, fuente: world.geo.json / Natural Earth) y algunas
    /// cabeceras departamentales como referencia (coordenadas aproximadas).
    /// </summary>
    public static class MapaBase
    {
        /// <summary>Contorno del país como pares { latitud, longitud }.</summary>
        public static readonly double[,] Contorno =
        {
            { 13.735338, -90.095555 },
            { 13.909771, -90.608624 },
            { 13.927832, -91.23241 },
            { 14.126218, -91.689747 },
            { 14.538829, -92.22775 },
            { 14.830103, -92.20323 },
            { 15.064585, -92.087216 },
            { 15.251447, -92.229249 },
            { 16.066565, -91.74796 },
            { 16.069562, -90.464473 },
            { 16.41011, -90.438867 },
            { 16.470778, -90.600847 },
            { 16.687483, -90.711822 },
            { 16.918477, -91.08167 },
            { 17.252177, -91.453921 },
            { 17.254658, -91.002269 },
            { 17.817595, -91.00152 },
            { 17.819326, -90.067934 },
            { 17.808319, -89.14308 },
            { 17.015577, -89.150806 },
            { 15.886938, -89.229122 },
            { 15.887273, -88.930613 },
            { 15.70638, -88.604586 },
            { 15.855389, -88.518364 },
            { 15.727722, -88.225023 },
            { 15.346247, -88.68068 },
            { 15.066419, -89.154811 },
            { 14.874286, -89.22522 },
            { 14.678019, -89.145535 },
            { 14.424133, -89.353326 },
            { 14.362586, -89.587343 },
            { 14.244816, -89.534219 },
            { 14.134228, -89.721934 },
            { 13.88197, -90.064678 },
            { 13.735338, -90.095555 }
        };

        public static readonly string[] CiudadesNombre =
        {
            "Ciudad de Guatemala", "Antigua Guatemala", "Escuintla", "Quetzaltenango", "Huehuetenango", "Cobán",
            "Flores", "Puerto Barrios", "Zacapa", "Retalhuleu", "Chiquimula", "Jalapa"
        };

        public static readonly double[,] CiudadesCoord =
        {
            { 14.634, -90.507 }, { 14.557, -90.733 }, { 14.305, -90.786 }, { 14.834, -91.518 },
            { 15.319, -91.470 }, { 15.470, -90.371 }, { 16.931, -89.893 }, { 15.727, -88.594 },
            { 14.972, -89.529 }, { 14.534, -91.677 }, { 14.801, -89.544 }, { 14.633, -89.988 }
        };
    }
}
