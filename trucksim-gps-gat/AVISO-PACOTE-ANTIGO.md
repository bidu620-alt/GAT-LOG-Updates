# Pacote legado retirado em 12/11/2026

O ZIP 1.0 foi detectado como ameaca pelo Microsoft Defender. A causa exata nao foi determinada. Os tres downloads binarios legados foram retirados da branch principal; o historico Git permanece disponivel para auditoria. Nao restaure nem recomende o pacote antigo.

O manifesto aponta para o pacote 1.4.4 recompilado do fonte correspondente, com testes de danos e verificacao do Defender documentados em releases/TruckSimGPS_1.4.4_BiduLog_Danos_VERIFICACAO.json. Essa verificacao nao constitui garantia absoluta de seguranca.

O workflow build-trucksim-gps-gat.yml e legado e nao deve republicar o pacote 1.0. Sua substituicao deve fixar a base 1.4.4 e exigir testes e verificacao de seguranca antes da publicacao.
