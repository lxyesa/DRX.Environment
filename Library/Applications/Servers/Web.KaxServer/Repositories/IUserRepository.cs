using System.Collections.Generic;
using Web.KaxServer.Models.Domain;

namespace Web.KaxServer.Repositories
{
    public interface IUserRepository
    {
        User GetById(int userId);
        IEnumerable<User> GetAll();
        User GetByUsername(string username);
        void Save(User user);
        void Delete(int userId);
    }
} 