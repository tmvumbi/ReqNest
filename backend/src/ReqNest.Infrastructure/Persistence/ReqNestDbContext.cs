using Microsoft.EntityFrameworkCore;

namespace ReqNest.Infrastructure.Persistence;

public sealed class ReqNestDbContext(DbContextOptions<ReqNestDbContext> options)
    : DbContext(options);
