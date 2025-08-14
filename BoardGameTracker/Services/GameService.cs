using BoardGameTracker.Models;
using BoardGameTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace BoardGameTracker.Services
{
    public class GameService : IGameService
    {
        private readonly ApplicationDbContext _context;

        public GameService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Game>> GetAllAsync()
        {
            return await _context.Games.ToListAsync();
        }

        public async Task<Game?> GetByIdAsync(int id)
        {
            return await _context.Games.FindAsync(id);
        }

        public async Task<IEnumerable<Game>> GetByOwnerAsync(string owner)
        {
            return await _context.Games
                .Where(g => g.Owner == owner)
                .OrderBy(g => g.Name)
                .ToListAsync();
        }

        public async Task<Game> AddAsync(Game game)
        {
            _context.Games.Add(game);
            await _context.SaveChangesAsync();
            return game;
        }

        public async Task<Game> UpdateAsync(Game game)
        {
            _context.Games.Update(game);
            await _context.SaveChangesAsync();
            return game;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var game = await _context.Games.FindAsync(id);
            if (game == null) return false;

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Games.AnyAsync(g => g.Id == id);
        }
    }
}