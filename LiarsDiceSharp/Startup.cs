using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiarsDiceSharp.Hubs;
using LiarsDiceSharp.LogicEngines;
using LiarsDiceSharp.Models.Contexts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LiarsDiceSharp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<GameContext>(options => options.UseSqlite(Configuration.GetConnectionString("LiarsDice")));
            services.AddScoped<GameLogic>();
            services.AddSignalR()
                .AddJsonProtocol();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseHttpsRedirection();

            app.UseRouting();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<LobbyHub>("/liarsDice");
            });

            app.UseDefaultFiles();
            
            app.UseStaticFiles();
        }
    }
}