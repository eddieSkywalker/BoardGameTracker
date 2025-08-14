using BoardGameTracker.Models;

namespace BoardGameTracker.Services
{
    public interface IGameService
    {
        Task<IEnumerable<Game>> GetAllAsync();
        Task<Game?> GetByIdAsync(int id);
        Task<IEnumerable<Game>> GetByOwnerAsync(string owner);
        Task<Game> AddAsync(Game game);
        Task<Game> UpdateAsync(Game game);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
    }
}