﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using compasso.Data;

using compasso.Models.Identity;
using compasso.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace compasso
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddCors();

            services.AddMvc();

            services.AddCors(m => m.AddDefaultPolicy(c=>
            {
                c.AllowAnyOrigin();
                c.AllowAnyHeader();
                c.AllowAnyMethod();
            }));
            

            services.AddDbContextPool<CompassoDbContext>(
                //options => options.UseMySql("Server=mysql.tomjbg.kinghost.net;Database=tomjbg;User=tomjbg;Password=ImagComp18;")
                options => options.UseMySql("Server=localhost;Database=tomjbg;User=tomjbg;Password=ImagComp18;")
                //options => options.UseMySql("Server=179.188.16.44;Database=compassdb;User=compassdb;Password=ImagComp18;")
            );
            services.AddDbContextPool<ApplicationDbContext>(
                //options => options.UseMySql("Server=mysql.tomjbg.kinghost.net;Database=tomjbg;User=tomjbg;Password=ImagComp18;")
                options => options.UseMySql("Server=localhost;Database=tomjbg;User=tomjbg;Password=ImagComp18;")
                //options => options.UseMySql("Server=179.188.16.44;Database=compassdb;User=compassdb;Password=ImagComp18;")
            );
            
            // Ativando o ASP.NET Identity
            services.AddIdentity<ApplicationUser, IdentityRole>(options => {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequiredUniqueChars = 0;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric=false;
                options.Password.RequireUppercase=false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // Add DI Middleware
            // AddTransient -> Criado uma nova instância sempre que são solicitados. Usados mais para serviços mais leves (EmailServices)
            // AddScoped -> Criado uma nova instância se já não tiver um na memória. (DatabaseContext)
            // AddSingleton -> Criados na primeira solicitação, solicitações subsequente usa a mesma instância

            // Configurando a dependência para a classe de validação de credenciais e geração de tokens
            services.AddScoped<AccessManager, AccessManager>();

            var signingConfigurations = new SigningConfigurations();
            services.AddSingleton(signingConfigurations);

            var tokenConfigurations = new TokenConfigurations();
            new ConfigureFromConfigurationOptions<TokenConfigurations>(Configuration.GetSection("TokenConfigurations"))
                .Configure(tokenConfigurations);
            
            services.AddSingleton(tokenConfigurations);

            // Adiciona a extensão que irá configurar o uso de 
            // autenticação e autorização via tokens
            // services.AddJwtSecurity(signingConfigurations, tokenConfigurations); // Usando a Extension personalizada JwtSecurityExtension

            // autenticação e autorização jwt na mão, direto na Startup.cs
            services.AddAuthentication(authOptions => {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(bearerOptions => {
                bearerOptions.TokenValidationParameters.IssuerSigningKey = signingConfigurations.Key;
                bearerOptions.TokenValidationParameters.ValidAudience = tokenConfigurations.Audience;
                bearerOptions.TokenValidationParameters.ValidIssuer = tokenConfigurations.Issuer;
                // Valida a assinatura de um token recebido ?
                bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey = true;
                // Verifica se um token recebido ainda é válido ?
                bearerOptions.TokenValidationParameters.ValidateLifetime = true;
                // Tempo de tolerância para a expiração de um token
                bearerOptions.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
            });
            
            // Ativa o uso do token como forma de autorização
            services.AddAuthorization(authOptions => {
                authOptions.AddPolicy("Bearer", new AuthorizationPolicyBuilder()
                            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                            .RequireAuthenticatedUser().Build());
            });
            

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, 
                              ApplicationDbContext dbcontext, 
                              CompassoDbContext dbcontext2,
                              UserManager<ApplicationUser> userManager, 
                              RoleManager<IdentityRole> roleManager)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            
            //policy.WithOrigins("*").AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();

            app.Use(async (context, next) => {
                await next();
                if (!Path.HasExtension(context.Request.Path.Value) &&
                    context.Response.StatusCode == 404)
                {
                    context.Request.Path = "/index.html";
                    await next();
                }

            });
            
            // Inicializando o banco de dados com usuários e permissões caso não existam
            // new IdentityInitializer(dbcontext, userManager, roleManager).Initialize();

            app.UseCors(c =>
            {
                c.AllowAnyHeader();
                c.AllowAnyMethod();
                c.AllowAnyOrigin();
                c.AllowCredentials();
            });

            app.UseStaticFiles();
            app.UseAuthentication();                        
            app.UseMvc();

        }
    }
}
