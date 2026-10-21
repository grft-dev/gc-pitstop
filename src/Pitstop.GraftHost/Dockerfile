FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /usr/app

RUN curl -fsSL grft.dev/get/gg | sh && dpkg -i gg.deb && rm gg.deb

COPY . /usr/app/
RUN dotnet publish -c Release -o /usr/app/out

WORKDIR /usr/app/out
EXPOSE 80
EXPOSE 81

CMD ["sh", "-c", "if [ -n \"$GG_PROJECT_KEY\" ]; then gg --modules Pitstop.GraftHost.dll --projectKey \"$GG_PROJECT_KEY\"; else gg --modules Pitstop.GraftHost.dll; fi"]
