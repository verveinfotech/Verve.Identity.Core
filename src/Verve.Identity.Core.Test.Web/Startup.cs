using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

using Verve.Identity.Core.Model;
using Verve.Identity.Core.Service.Configuration;
using Verve.Identity.Core.Service.Extensions;
using Verve.Identity.Core.Test.ApplicationDbContext;
using Verve.Identity.Core.Test.Entity;
using Verve.Identity.Core.Test.Web.Service;

namespace Verve.Identity.Core.Test.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            var connectionString = Configuration.GetSection("SqlServer:ConnectionString").Get<string>();

            services.AddDbContext<TestApplicationDbContext>(builder => builder.UseSqlServer(connectionString));

            services.AddScoped<IUserService, UserService>();
            
            services.AddVerveIdentityServices<TestApplicationDbContext, UserAccount, VerveRole>();

            ConfigureAuthentication(services);
        }

        private void ConfigureAuthentication(IServiceCollection services)
        {
            services.AddVerveIdentity<UserAccount, VerveRole>()
                .AddDefaultTokenProviders();

            services.Configure<IdentitySettings>(Configuration.GetSection("IdentitySettings"));

            var secretKey = Configuration.GetSection("IdentitySettings:SecretKey").Get<string>();

            var key = Encoding.ASCII.GetBytes(secretKey);

            services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }
            )
            .AddJwtBearer(j =>
                {
                    j.RequireHttpsMetadata = true;
                    j.SaveToken = true;
                    j.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                    j.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                            {
                                context.Response.Headers.Add("Token-Expired", "true");
                            }
                            return Task.CompletedTask;
                        }
                    };
                }
            );

            //services.AddAuthorization(options =>
            //    {
            //        options.AddPolicy("CommunityMemberAllowed", configure =>
            //        {
            //            configure.RequireClaim("ITSNumber");
            //        });
            //    }
            //);

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
