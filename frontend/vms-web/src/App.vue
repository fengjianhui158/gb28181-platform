<template>
  <el-container style="height: 100vh">
    <el-aside width="200px" class="tech-sidebar">
      <div class="logo">X-Link 安联智体</div>
      <el-menu
        :default-active="route.path"
        router
        class="tech-menu"
        background-color="transparent"
        text-color="var(--tech-text-secondary)"
        active-text-color="var(--tech-primary)"
      >
        <el-menu-item index="/devices">
          <el-icon><Monitor /></el-icon>
          <span>设备管理</span>
        </el-menu-item>
        <el-menu-item index="/preview">
          <el-icon><VideoCamera /></el-icon>
          <span>实时预览</span>
        </el-menu-item>
        <el-menu-item index="/diagnostic">
          <el-icon><FirstAidKit /></el-icon>
          <span>诊断面板</span>
        </el-menu-item>
        <el-menu-item index="/ai-chat">
          <el-icon><ChatDotRound /></el-icon>
          <span>AI 问答</span>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-main class="tech-main">
      <router-view />
    </el-main>
  </el-container>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
const route = useRoute()
</script>

<style scoped>
body {
  margin: 0;
  font-family: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
}

.tech-sidebar {
  background: var(--tech-bg-elevated);
  border-right: 1px solid var(--tech-border);
}

.logo {
  height: 60px;
  line-height: 60px;
  text-align: center;
  color: var(--tech-primary);
  font-size: 18px;
  font-weight: bold;
  border-bottom: 1px solid var(--tech-border);
  text-shadow: 0 0 10px rgba(0, 212, 255, 0.3);
}

.tech-menu {
  border-right: none;
}

.tech-menu ::v-deep(.el-menu-item) {
  border-left: 3px solid transparent;
  transition: all 0.3s ease;
}

.tech-menu ::v-deep(.el-menu-item:hover) {
  border-left-color: var(--tech-primary);
  background-color: var(--tech-bg-hover) !important;
}

.tech-menu ::v-deep(.el-menu-item.is-active) {
  background-color: var(--tech-bg-hover) !important;
  border-left-color: var(--tech-primary);
  box-shadow: inset -4px 0 8px rgba(0, 212, 255, 0.1);
}

.tech-menu ::v-deep(.el-icon) {
  margin-right: 8px;
}

.tech-main {
  background: var(--tech-bg-base);
  padding: 20px;
}
</style>
