namespace Restaurapp.BlazorServer.Models
{
    public class EstoqueInsumo
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public int InsumoId { get; set; }
        public int Quantidade { get; set; }
        public Insumo Insumo { get; set; } = null!;
    }
}