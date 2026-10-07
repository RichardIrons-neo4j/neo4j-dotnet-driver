ARG BASE_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
FROM ${BASE_IMAGE} AS build

ENV TZ=Europe/London

RUN command -v python3 > /dev/null \
	|| { apt -o Acquire::Retries=5 -o Acquire::Languages=none update \
	&& DEBIAN_FRONTEND=noninteractive apt -o Acquire::Retries=5 install -y --no-install-recommends python3 \
	&& rm -rf /var/lib/apt/lists/*; }

# install .NET runtimes
RUN wget https://dot.net/v1/dotnet-install.sh \
	&& bash dotnet-install.sh --runtime aspnetcore --channel 8.0 --install-dir /usr/share/dotnet \
	&& bash dotnet-install.sh --runtime aspnetcore --channel 9.0 --install-dir /usr/share/dotnet \
	&& bash dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet \
	&& rm dotnet-install.sh

ENV PYTHON=python3
ENV DOTNET_DRIVER_USING_LOCAL_SERVER=true
ENV TEST_NEO4J_USING_TESTKIT=true
ENV TK_CUSTOM_CA_PATH="/usr/local/share/custom-ca-certificates/"

# Install our own CAs on the image.
# Assumes Linux Debian based image.
COPY CAs/* /usr/local/share/ca-certificates/
RUN update-ca-certificates

COPY CustomCAs/* /usr/local/share/custom-ca-certificates/

# controls which config the testkit backend loads
ENV ASPNETCORE_ENVIRONMENT=ci