"use client";

import { Button } from "@astryxdesign/core/Button";
import {
  Card,
  Divider,
  Heading,
  HStack,
  InputGroup,
  InputGroupText,
  Layout,
  LayoutContent,
  LayoutFooter,
  LayoutHeader,
  Switch,
  TextInput,
  VStack,
} from "@astryxdesign/core";
import { useState } from "react";
import { IconMailQuestion, IconPassword, IconUserKey } from "@tabler/icons-react";

export default function Home() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(false);

  return (
    <div className="min-h-dvh flex items-center justify-center font-sans px-4 py-10">
      <main className="w-full max-w-sm flex flex-col items-center gap-6 sm:gap-8">
        <Card className="bg-violet-800! text-white text-2xl font-bold border-none! flex items-center justify-center size-20 sm:size-24">
          .NET
        </Card>

        <Card className="w-full">
          <Layout
            header={
              <LayoutHeader hasDivider>
                <HStack gap={3} className="items-center">
                  <IconUserKey />
                  <Heading level={3}>.NET Core Kimlik Doğrulama</Heading>
                </HStack>
              </LayoutHeader>
            }
            content={
              <LayoutContent>
                <VStack gap={4}>
                  <InputGroup label="E-posta">
                    <InputGroupText>
                      <IconMailQuestion />
                    </InputGroupText>
                    <TextInput label="Email"
                      type="email"
                      description="Onay bağlantısı bu adrese gönderilecek"
                      value={email}
                      onChange={setEmail}
                      placeholder="ornek@mail.com"
                    />
                  </InputGroup>

                  <InputGroup label="Şifre">
                    <InputGroupText>
                      <IconPassword />
                    </InputGroupText>
                    <TextInput label="Şifre"
                      type="password"
                      description="En az 8 karakter ve bir büyük harf"
                      value={password}
                      onChange={setPassword}
                      placeholder="Şifreni gir"
                    />
                  </InputGroup>

                  <Switch
                    label="Beni hatırla"
                    description="Bu cihazda oturumu açık tut"
                    value={remember}
                    onChange={setRemember}
                  />

                  <Button label="Giriş Yap" className="w-full" variant="primary">
                    Giriş Yap
                  </Button>

                  <Divider label="veya" />

                  <Button label="Kayıt Ol" className="w-full" variant="secondary">
                    Kayıt Ol
                  </Button>
                </VStack>
              </LayoutContent>
            }
            footer={<LayoutFooter />}
          />
        </Card>
      </main>
    </div>
  );
}