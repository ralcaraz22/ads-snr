using Quartz;
using ads.Repository;


var builder = WebApplication.CreateBuilder(args);

//Quartz run for cronjob
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    var jobKey = new JobKey("DataRepo");
    q.AddJob<DataRepo>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("DataRepo-trigger")
       //.WithCronSchedule("35 21 08 * * ?"));
    ////Actual Record of Final Records
    .WithCronSchedule("01 00 06 * * ?"));

});



builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Web Api",
        Version = "v1",
    });

    // Add this line to modify the controller names in Swagger
    options.CustomSchemaIds(type => type.FullName);
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowOrigin", policy =>
    {
        policy.AllowAnyHeader()
                .AllowAnyMethod() //allow any http methods
                .SetIsOriginAllowed(isOriginAllowed: _ => true) //no restriction in any domain
                .AllowCredentials();
    });
});

var app = builder.Build();

app.UseRouting();
app.UseCors(options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ADS");

    });
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();
