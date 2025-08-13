FROM ubuntu:22.04

ARG PROTOBUF_VERSION=25.3
ENV PROTOBUF_VERSION=${PROTOBUF_VERSION}

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    unzip \
    ca-certificates \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/

# Install protoc binary from GitHub release page
RUN curl -LO "https://github.com/protocolbuffers/protobuf/releases/download/v${PROTOBUF_VERSION}/protoc-${PROTOBUF_VERSION}-linux-x86_64.zip" && \
    unzip "protoc-${PROTOBUF_VERSION}-linux-x86_64.zip" -d /usr/local && \
    rm "protoc-${PROTOBUF_VERSION}-linux-x86_64.zip"

WORKDIR /app
