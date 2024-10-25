# Generation of certificates using openssl

For more information read the [mosquitto-tls man page](https://mosquitto.org/man/mosquitto-tls-7.html).

## Root Certificate Authority

```shell
openssl req -new -newkey rsa:4096 -x509 -days 36500 -extensions v3_ca -keyout self_signed_root_ca.key -out self_signed_root_ca.crt -passout pass:self_signed_root_ca_pass -subj "/C=ZZ/ST=Milky Way/L=Earth/O=MqttSql/OU=Test CA/CN=self_signed_root_ca"
```

## Intermediate Certificate Authority (doesn't work, TODO)

```shell
openssl genrsa -aes256 -out self_signed_ca.key -passout pass:self_signed_ca_pass 4096
openssl req -new -out self_signed_ca.crt -key self_signed_ca.key -passin pass:self_signed_ca_pass -x509 -days 36500 -sha256 -extensions v3_ca -CA self_signed_root_ca.crt -CAkey self_signed_root_ca.key -passin pass:self_signed_root_ca_pass -CAcreateserial -subj "/C=ZZ/ST=Milky Way/L=Earth/O=MqttSql/OU=Test CA/CN=self_signed_ca"
```

## Server

- Password protected keyfile

    ```shell
    openssl genrsa -aes256 -out server.key -passout pass:server_pass 2048
    openssl req -new -out server.csr -key server.key -passin pass:server_pass -subj "/C=ZZ/ST=Milky Way/L=Earth/O=MqttSql/OU=Test Server/CN=server"
    openssl x509 -req -in server.csr -CA self_signed_ca.crt -CAkey self_signed_ca.key -passin pass:self_signed_ca_pass -CAcreateserial -out server.crt -days 36500
    ```

- Unencrypted keyfile

    ```shell
    openssl genrsa -out server.key 2048
    openssl req -new -out server.csr -key server.key -subj "/C=ZZ/ST=Milky Way/L=Earth/O=MqttSql/OU=Test Server/CN=server"
    openssl x509 -req -in server.csr -CA self_signed_ca.crt -CAkey self_signed_ca.key -passin pass:self_signed_ca_pass -CAcreateserial -out server.crt -days 36500
    ```

## Client

- Password protected

    ```shell
    openssl genrsa -aes256 -out client.key -passout pass:client_pass 2048
    openssl req -new -out client.csr -key client.key -passin pass:client_pass -subj "/C=ZZ/ST=Milky Way/L=Earth/O=MqttSql/OU=Test Client/CN=client"
    openssl x509 -req -in client.csr -CA self_signed_ca.crt -CAkey self_signed_ca.key -passin pass:self_signed_ca_pass -CAserial self_signed_ca.srl -out client.crt -days 36500
    openssl pkcs12 -export -out client.pfx -inkey client.key -in client.crt -passin pass:client_pass -passout pass:client_pass
    ```

- Password protected

    ```shell
    openssl genrsa -out client.key 2048
    openssl req -new -out client.csr -key client.key -subj "/C=ZZ/ST=Milky Way/L=Earth/O=MqttSql/OU=Test Client/CN=client"
    openssl x509 -req -in client.csr -CA self_signed_ca.crt -CAkey self_signed_ca.key -passin pass:self_signed_ca_pass -CAserial self_signed_ca.srl -out client.crt -days 36500
    openssl pkcs12 -export -out client.pfx -inkey client.key -in client.crt -passout pass:
    ```

## Diffie Hellman parameters

```shell
openssl dhparam -out dhparam.pem 2048
```
