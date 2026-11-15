using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.Filters;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers(options =>
            {
                // Every request must say which user it is acting as (see CurrentUserFilter)
                options.Filters.Add<CurrentUserFilter>(CurrentUserFilter.FilterOrder);
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                // Adds an "Authorize" button to Swagger where a user id can be typed once.
                // Swagger then sends it as the X-User-Id header, the same way the MVC app does.
                options.AddSecurityDefinition("ActingUser", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = ApiHeaders.UserId,
                    Description = "Id of the user to act as, for example 1."
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("ActingUser", document)] = []
                });
            });

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
            });

            // The clock is injected so the time-based rules can be tested
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

            // Switches that depend on where the application runs, such as whether new teams may register
            builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.Section));

            // Film: where video files are kept, and the ffmpeg programs that read and make them.
            // "Film:Storage" chooses between a folder on this computer and an Amazon S3 bucket.
            builder.Services.Configure<FilmOptions>(builder.Configuration.GetSection(FilmOptions.Section));
            builder.Services.AddSingleton<IFilmStorage>(services =>
            {
                var options = services.GetRequiredService<IOptions<FilmOptions>>();

                return string.Equals(options.Value.Storage, "S3", StringComparison.OrdinalIgnoreCase)
                    ? new S3FilmStorage(options)
                    : new LocalFilmStorage(options, services.GetRequiredService<IHostEnvironment>());
            });
            builder.Services.AddSingleton<IVideoTools, FfmpegVideoTools>();

            // Cutups are made into videos in the background, one at a time (see CutupRenderWorker)
            builder.Services.AddScoped<CutupRenderer>();
            builder.Services.AddHostedService<CutupRenderWorker>();

            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<ITeamService, TeamService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IRosterService, RosterService>();
            builder.Services.AddScoped<IPositionGroupService, PositionGroupService>();
            builder.Services.AddScoped<IDepthChartService, DepthChartService>();
            builder.Services.AddScoped<IScheduleService, ScheduleService>();
            builder.Services.AddScoped<IAttendanceService, AttendanceService>();
            builder.Services.AddScoped<PlaybookChangeApplier>();
            builder.Services.AddScoped<IPlaybookService, PlaybookService>();
            builder.Services.AddScoped<IApprovalService, ApprovalService>();
            builder.Services.AddScoped<IScoutedTeamService, ScoutedTeamService>();
            builder.Services.AddScoped<IGameService, GameService>();
            builder.Services.AddScoped<IVideoService, VideoService>();
            builder.Services.AddScoped<IClipService, ClipService>();
            builder.Services.AddScoped<ICutupService, CutupService>();
            builder.Services.AddScoped<ITendencyService, TendencyService>();
            builder.Services.AddScoped<IStudyTimeService, StudyTimeService>();
            builder.Services.AddScoped<IActivityService, ActivityService>();

            var app = builder.Build();

            // Create or update the database, then add the demo teams when Seed:DemoData is switched on
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Database.Migrate();

                var demoPassword = app.Configuration["Seed:DemoPassword"];
                if (app.Configuration.GetValue<bool>("Seed:DemoData") && !string.IsNullOrWhiteSpace(demoPassword))
                {
                    DbSeeder.Seed(context, demoPassword, TimeProvider.System);
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
